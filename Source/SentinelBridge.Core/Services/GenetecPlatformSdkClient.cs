using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SentinelBridge.Core.Configuration;
using SentinelBridge.Core.Interfaces;
using SentinelBridge.Core.Models;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace SentinelBridge.Core.Services;

[SupportedOSPlatform("windows")]
public class GenetecPlatformSdkClient : IGenetecEventSource, IDisposable
{
    private readonly ILogger<GenetecPlatformSdkClient> _logger;
    private readonly IConfigurationManager _configurationManager;
    private GenetecSdkConfiguration _options;
    private readonly ConcurrentQueue<PlatformSdkEventEnvelope> _eventQueue = new();
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly SemaphoreSlim _connectionSemaphore = new(1, 1);
    private readonly GenetecAuthenticationService _authenticationService;
    private readonly ServiceOperationalStatusStore? _operationalStatusStore;
    private Task? _eventProcessingTask;
    private HashSet<string> _selectedEventTypes = new(StringComparer.OrdinalIgnoreCase);
    private int _queuedEventCount;
    private const int MaxQueuedEvents = 10_000;

    private bool _eventsSubscribed = false;
    private bool _isConnected;
    private bool _disposed;

    private sealed record PlatformSdkEventEnvelope(
        int EventTypeId,
        string EventTypeName,
        Guid SourceGuid,
        DateTimeOffset Timestamp,
        string? GroupId,
        object? SpecificEvent);

    public event EventHandler<GenetecEvent>? EventReceived;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public bool IsConnected => _authenticationService.IsAuthenticated;
    public bool IsSubscribed => IsConnected && _eventsSubscribed;
    public int SelectedEventCount => _selectedEventTypes.Count;

    internal void UpdateConfiguration(GenetecSdkConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (_isConnected)
        {
            throw new InvalidOperationException("Platform SDK configuration cannot change while connected.");
        }

        _options = configuration;
        _authenticationService.UpdateConfiguration(configuration);
    }

    // Touch the SDK type only in SDK-enabled builds. Availability errors are
    // reported by StartAsync instead of poisoning the whole .NET type.
    static GenetecPlatformSdkClient()
    {
#if GENETEC_SDK_AVAILABLE
        _ = typeof(Genetec.Sdk.Engine);
#endif
    }

    public GenetecPlatformSdkClient(
        ILogger<GenetecPlatformSdkClient> logger,
        IConfigurationManager configurationManager,
        IOptions<GenetecSdkConfiguration> options,
        GenetecAuthenticationService authenticationService,
        ServiceOperationalStatusStore? operationalStatusStore = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        _operationalStatusStore = operationalStatusStore;

        _authenticationService.AuthenticationStatusChanged += OnAuthenticationStatusChanged;
        _authenticationService.AuthenticationFailed += OnAuthenticationFailed;

        _logger.LogInformation("Using Genetec Platform SDK with dedicated authentication service");
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Platform SDK client initialization - delegating to StartAsync");
        return Task.CompletedTask;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connectionSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_isConnected)
            {
                _logger.LogInformation("Platform SDK client is already connected");
                return;
            }

            _logger.LogInformation("Starting Genetec Platform SDK client - connecting to {Server}:{Port}",
                _options.DirectoryServer, _options.Port);

            _operationalStatusStore?.SetPhase(
                ServiceOperationalPhase.LoadingSdk,
                "Comprobando Genetec SDK 5.12...");
            if (!IsGenetecSdkAvailable())
            {
                throw new FileNotFoundException(
                    "Genetec Platform SDK is not installed. Please install the matching SDK version on this machine.");
            }

            _operationalStatusStore?.SetPhase(
                ServiceOperationalPhase.Authenticating,
                "Autenticando con Genetec Security Center...",
                sdkLoaded: true,
                authenticated: false,
                subscribed: false,
                readyToPublish: false);
            var authResult = await _authenticationService.AuthenticateAsync(cancellationToken);
            if (!authResult)
            {
                var authException = _authenticationService.LastAuthenticationException;
                if (authException != null)
                {
                    var rootMessage = authException.GetBaseException().Message;
                    throw new GenetecAuthenticationException(
                        $"Failed to authenticate with Security Center: {rootMessage}",
                        authException);
                }

                throw new GenetecAuthenticationException("Failed to authenticate with Security Center");
            }

            _operationalStatusStore?.SetPhase(
                ServiceOperationalPhase.Subscribing,
                "Autenticado. Suscribiendo los eventos seleccionados...",
                sdkLoaded: true,
                authenticated: true,
                subscribed: false,
                readyToPublish: false);
            await SubscribeToEventsAsync(cancellationToken);

            if (!_eventsSubscribed || _selectedEventTypes.Count == 0)
            {
                throw new ConfigurationException(
                    "No hay eventos seleccionados. El servicio no puede quedar operativo sin una suscripcion activa.");
            }

            _operationalStatusStore?.SetPhase(
                ServiceOperationalPhase.Subscribing,
                $"Autenticado y suscrito a {_selectedEventTypes.Count} eventos. Preparando el envio...",
                sdkLoaded: true,
                authenticated: true,
                subscribed: true,
                selectedEventCount: _selectedEventTypes.Count,
                readyToPublish: false);

            if (_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();
            }
            _eventProcessingTask = Task.Run(ProcessEventsAsync, _cancellationTokenSource.Token);

            _logger.LogInformation("Successfully started Genetec Platform SDK client");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start Genetec Platform SDK client: {ErrorType}: {ErrorMessage}",
                ex.GetType().Name, ex.Message);
            _operationalStatusStore?.SetPhase(
                ServiceOperationalPhase.Faulted,
                $"No se pudo iniciar la integracion con Genetec: {ex.Message}",
                authenticated: _authenticationService.IsAuthenticated,
                subscribed: false,
                readyToPublish: false,
                lastError: ex.Message);
            await CleanupConnectionAsync();
            if (_authenticationService.IsAuthenticated)
            {
                await _authenticationService.LogoutAsync();
            }
            throw;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _connectionSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_isConnected)
            {
                _logger.LogInformation("Platform SDK client is already stopped");
                return;
            }

            _logger.LogInformation("Stopping Genetec Platform SDK client");

            _cancellationTokenSource.Cancel();

            if (_eventProcessingTask != null)
            {
                try
                {
                    await _eventProcessingTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("Timed out waiting for the Platform SDK event processor to stop");
                }
                finally
                {
                    _eventProcessingTask = null;
                }
            }

            await CleanupConnectionAsync();
            await _authenticationService.LogoutAsync();

            _logger.LogInformation("Successfully stopped Genetec Platform SDK client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Platform SDK client shutdown");
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private bool IsGenetecSdkAvailable()
    {
#if GENETEC_SDK_AVAILABLE
        return true;
#else
        return false;
#endif
    }

    private Task SubscribeToEventsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Subscribing to Platform SDK events");

#if GENETEC_SDK_AVAILABLE
            var engine = _authenticationService.Engine;
            if (engine != null)
            {
                if (!_eventsSubscribed)
                {
                    _selectedEventTypes = GenetecEventCatalog.ExpandSelection(
                        _configurationManager.GetConfiguration().Genetec.EventTypes);
                    if (_selectedEventTypes.Count > 0)
                    {
                        engine.EventReceived += OnEngineEventReceived;
                        _eventsSubscribed = true;
                    }
                }
                if (_eventsSubscribed)
                    _logger.LogInformation("Platform SDK event subscription active for {SelectedCount} exact event types",
                        _selectedEventTypes.Count);
                else
                    _logger.LogWarning("No Genetec event types are selected; the SDK will not forward events");
            }
            else
            {
                throw new InvalidOperationException("Engine is not properly initialized for event subscription");
            }
            return Task.CompletedTask;
#else
            throw new InvalidOperationException("Genetec SDK is not available for compilation");
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to events");
            throw new GenetecAuthenticationException("Failed to subscribe to events", ex);
        }
    }

    private async Task ProcessEventsAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;

        _logger.LogDebug("Started event processing task");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_eventQueue.TryDequeue(out var envelope))
                {
                    Interlocked.Decrement(ref _queuedEventCount);
                    var genetecEvent = ConvertSdkEventToGenetecEvent(envelope);
                    EventReceived?.Invoke(this, genetecEvent);
                }
                else
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing events");
                ErrorOccurred?.Invoke(this, ex);
                await Task.Delay(1000, cancellationToken);
            }
        }

        _logger.LogDebug("Event processing task completed");
    }

#if GENETEC_SDK_AVAILABLE
    private void OnEngineEventReceived(object? sender, Genetec.Sdk.EventReceivedEventArgs args)
    {
        try
        {
            var sdkEventName = args.EventType.ToString();
            var definition = GenetecEventCatalog.Find(sdkEventName);
            var selectionId = definition?.Id ?? sdkEventName;
            if (!_selectedEventTypes.Contains(selectionId))
            {
                _logger.LogTrace("Ignoring unselected SDK event {SdkEventType} from {SourceGuid}",
                    sdkEventName, args.SourceGuid);
                return;
            }

            if (Interlocked.Increment(ref _queuedEventCount) > MaxQueuedEvents)
            {
                Interlocked.Decrement(ref _queuedEventCount);
                _logger.LogWarning("Platform SDK event queue full; dropping {SdkEventType} from {SourceGuid}",
                    sdkEventName, args.SourceGuid);
                return;
            }

            _eventQueue.Enqueue(new PlatformSdkEventEnvelope(
                Convert.ToInt32(args.EventType),
                sdkEventName,
                args.SourceGuid,
                ConvertTimestamp(args.Timestamp),
                Convert.ToString(args.GroupId, System.Globalization.CultureInfo.InvariantCulture),
                args.Event));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process SDK event");
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    private GenetecEvent ConvertSdkEventToGenetecEvent(PlatformSdkEventEnvelope envelope)
    {
        var definition = GenetecEventCatalog.Find(envelope.EventTypeName);
        var stableKey = $"{envelope.EventTypeId}|{envelope.SourceGuid:D}|{envelope.Timestamp:O}|{envelope.GroupId}";
        var stableId = CreateStableGuid(stableKey);
        var entity = ResolveEntity(envelope.SourceGuid);
        var additionalData = ExtractSpecificEventData(envelope.SpecificEvent);
        additionalData["source"] = "PlatformSDK";
        additionalData["sdkCatalogVersion"] = GenetecEventCatalog.Version;
        additionalData["sdkEventType"] = envelope.EventTypeName;
        additionalData["sdkEventClass"] = envelope.SpecificEvent?.GetType().FullName ?? string.Empty;
        additionalData["groupId"] = envelope.GroupId ?? string.Empty;

        return new GenetecEvent
        {
            EventId = stableId,
            ExternalAlarmId = $"GEN_{stableId:N}",
            EventType = definition?.Category ?? EventType.Custom,
            Severity = definition?.DefaultSeverity ?? EventSeverity.Medium,
            SdkEventTypeId = envelope.EventTypeId,
            SdkEventTypeName = envelope.EventTypeName,
            SourceGuid = envelope.SourceGuid,
            SdkGroupId = envelope.GroupId,
            RawEventClass = envelope.SpecificEvent?.GetType().FullName,
            Timestamp = envelope.Timestamp,
            Description = definition?.DisplayName ?? envelope.EventTypeName,
            SourceEntity = entity,
            AdditionalData = additionalData
        };
    }

    private static Dictionary<string, object> ExtractSpecificEventData(object? specificEvent)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (specificEvent == null) return values;

        foreach (var property in specificEvent.GetType().GetProperties()
                     .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            try
            {
                var value = property.GetValue(specificEvent);
                if (value == null || value is string || value.GetType().IsValueType)
                {
                    values[$"event.{property.Name}"] = value ?? string.Empty;
                }
            }
            catch
            {
                // A single SDK property must not prevent delivery of the event.
            }
        }

        return values;
    }

    private EntityInfo ResolveEntity(Guid sourceGuid)
    {
        try
        {
            var entity = _authenticationService.Engine.GetEntity(sourceGuid);
            if (entity == null) return new EntityInfo { EntityId = sourceGuid, Name = sourceGuid.ToString("D"), Type = "Unknown" };
            var type = entity.GetType();
            return new EntityInfo
            {
                EntityId = sourceGuid,
                Name = Convert.ToString(type.GetProperty("Name")?.GetValue(entity)) ?? sourceGuid.ToString("D"),
                Type = Convert.ToString(type.GetProperty("EntityType")?.GetValue(entity)) ?? type.Name
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve SDK entity {SourceGuid}", sourceGuid);
            return new EntityInfo { EntityId = sourceGuid, Name = sourceGuid.ToString("D"), Type = "Unknown" };
        }
    }

    private static DateTimeOffset ConvertTimestamp(object timestamp) => timestamp switch
    {
        DateTimeOffset dto => dto,
        DateTime dt => new DateTimeOffset(dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt),
        _ => DateTimeOffset.UtcNow
    };

    private static Guid CreateStableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
#endif

#if !GENETEC_SDK_AVAILABLE
    private static GenetecEvent ConvertSdkEventToGenetecEvent(PlatformSdkEventEnvelope envelope) =>
        throw new InvalidOperationException("Genetec SDK is not available for event conversion");
#endif

    public Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
#if GENETEC_SDK_AVAILABLE
            if (_authenticationService.Engine is Genetec.Sdk.Engine)
            {
                var isConnected = _isConnected;
                _logger.LogInformation("Connectivity test result: {IsConnected}", isConnected);
                return Task.FromResult(isConnected);
            }
#endif

            _logger.LogWarning("Cannot test connectivity - SDK not available or engine not initialized");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test connectivity");
            return Task.FromResult(false);
        }
    }

    public Task<EntityInfo?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            _logger.LogWarning("Cannot get entity - not connected to Security Center");
            return Task.FromResult<EntityInfo?>(null);
        }

        try
        {
#if GENETEC_SDK_AVAILABLE
            if (_authenticationService.Engine is Genetec.Sdk.Engine)
            {
                return Task.FromResult<EntityInfo?>(ResolveEntity(entityId));
            }
#endif

            _logger.LogWarning("Entity {EntityId} not found or SDK not available", entityId);
            return Task.FromResult<EntityInfo?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get entity {EntityId}", entityId);
            return Task.FromResult<EntityInfo?>(null);
        }
    }

    public Task PrefetchEntitiesAsync(CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            _logger.LogWarning("Cannot prefetch entities - not connected");
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogDebug("Platform SDK entities are resolved from SourceGuid as events arrive");
        return Task.CompletedTask;
    }

    private Task CleanupConnectionAsync()
    {
        try
        {
            _logger.LogDebug("Cleaning up Platform SDK connection");

#if GENETEC_SDK_AVAILABLE
            if (_eventsSubscribed)
            {
                _authenticationService.Engine.EventReceived -= OnEngineEventReceived;
                _eventsSubscribed = false;
                _logger.LogInformation("Platform SDK event subscription removed");
            }
#endif

            _selectedEventTypes.Clear();
            _eventQueue.Clear();
            Interlocked.Exchange(ref _queuedEventCount, 0);

            _logger.LogDebug("Platform SDK connection cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during connection cleanup");
        }

        return Task.CompletedTask;
    }

    #region Authentication Service Event Handlers

    private void OnAuthenticationStatusChanged(object? sender, AuthenticationStatusChangedEventArgs e)
    {
        _isConnected = e.IsAuthenticated;
        _logger.LogInformation("Authentication status changed: {IsAuthenticated} - {Message}",
            e.IsAuthenticated, e.Message);
        ConnectionStatusChanged?.Invoke(this, e.IsAuthenticated);
    }

    private void OnAuthenticationFailed(object? sender, AuthenticationFailedEventArgs e)
    {
        _logger.LogError("Authentication failed on attempt {RetryAttempt}: {ErrorType}: {ErrorMessage}",
            e.RetryAttempt, e.Exception.GetType().Name, e.Exception.Message);
        _operationalStatusStore?.SetPhase(
            ServiceOperationalPhase.Faulted,
            $"Fallo de autenticacion con Genetec: {e.Exception.Message}",
            authenticated: false,
            subscribed: false,
            readyToPublish: false,
            lastError: e.Exception.Message);
        ErrorOccurred?.Invoke(this, e.Exception);
    }

    #endregion

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                _authenticationService.AuthenticationStatusChanged -= OnAuthenticationStatusChanged;
                _authenticationService.AuthenticationFailed -= OnAuthenticationFailed;

                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }

            _cancellationTokenSource.Dispose();
            _connectionSemaphore.Dispose();
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public class GenetecAuthenticationException : Exception
{
    public GenetecAuthenticationException(string message) : base(message) { }
    public GenetecAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}

public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class GenetecConnectionStateException : GenetecAuthenticationException
{
    public GenetecConnectionStateException(string connectionState)
        : base($"Security Center rechazo la autenticacion (estado del SDK: '{connectionState}').")
    {
        ConnectionState = connectionState;
    }

    public string ConnectionState { get; }
}

