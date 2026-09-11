using Microsoft.Extensions.Logging;
using SentinelBridge.Core.Interfaces;
using SentinelBridge.Core.Models;
using System.Runtime.Versioning;

namespace SentinelBridge.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class GenetecEventSource(
    ILogger<GenetecEventSource> logger,
    IConfigurationManager configurationManager,
    GenetecPlatformSdkClient platformSdkClient) : IGenetecEventSource
{
    private readonly ILogger<GenetecEventSource> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IConfigurationManager _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
    private readonly GenetecPlatformSdkClient _platformSdkClient = platformSdkClient ?? throw new ArgumentNullException(nameof(platformSdkClient));
    private bool _isInitialized;
    private bool _isStarted;
    private bool _disposed;

    public event EventHandler<GenetecEvent>? EventReceived;
    public event EventHandler<Exception>? ErrorOccurred;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public bool IsConnected { get; private set; }
    public bool IsSubscribed => IsConnected && _platformSdkClient.IsSubscribed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            var sdkConfiguration = _configurationManager.GetConfiguration().Genetec.Sdk;
            _platformSdkClient.UpdateConfiguration(sdkConfiguration);
            AttachClientEvents();
            await _platformSdkClient.InitializeAsync(cancellationToken);

            IsConnected = _platformSdkClient.IsConnected;
            _isInitialized = true;
            _logger.LogInformation("Genetec Platform SDK event source initialized");
        }
        catch (Exception ex)
        {
            DetachClientEvents();
            _logger.LogError(ex, "Failed to initialize the Genetec Platform SDK event source");
            ErrorOccurred?.Invoke(this, ex);
            throw;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Event source must be initialized before starting");
        }

        if (_isStarted)
        {
            return;
        }

        try
        {
            await _platformSdkClient.StartAsync(cancellationToken);
            IsConnected = _platformSdkClient.IsConnected;

            if (!IsConnected || !_platformSdkClient.IsSubscribed)
            {
                throw new InvalidOperationException(
                    "The Platform SDK did not reach an authenticated event subscription state");
            }

            _isStarted = true;
            _logger.LogInformation("Genetec Platform SDK is authenticated and listening for events");
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start Genetec event reception: {ErrorType}: {ErrorMessage}",
                ex.GetType().Name, ex.Message);
            ErrorOccurred?.Invoke(this, ex);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isInitialized)
        {
            return;
        }

        try
        {
            await _platformSdkClient.StopAsync(cancellationToken);
            _logger.LogInformation("Genetec event reception stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Genetec event reception");
            ErrorOccurred?.Invoke(this, ex);
            throw;
        }
        finally
        {
            _isStarted = false;
            _isInitialized = false;
            IsConnected = false;
            ConnectionStatusChanged?.Invoke(this, false);
            DetachClientEvents();
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _platformSdkClient.TestConnectivityAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Genetec Platform SDK connectivity test failed");
            return false;
        }
    }

    public async Task PrefetchEntitiesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _platformSdkClient.PrefetchEntitiesAsync(cancellationToken);
            _logger.LogInformation("Genetec entity prefetch completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to prefetch Genetec entities");
            ErrorOccurred?.Invoke(this, ex);
            throw;
        }
    }

    private void AttachClientEvents()
    {
        DetachClientEvents();
        _platformSdkClient.EventReceived += OnEventReceived;
        _platformSdkClient.ErrorOccurred += OnErrorOccurred;
        _platformSdkClient.ConnectionStatusChanged += OnConnectionStatusChanged;
    }

    private void DetachClientEvents()
    {
        _platformSdkClient.EventReceived -= OnEventReceived;
        _platformSdkClient.ErrorOccurred -= OnErrorOccurred;
        _platformSdkClient.ConnectionStatusChanged -= OnConnectionStatusChanged;
    }

    private void OnEventReceived(object? sender, GenetecEvent e) => EventReceived?.Invoke(this, e);

    private void OnErrorOccurred(object? sender, Exception ex) => ErrorOccurred?.Invoke(this, ex);

    private void OnConnectionStatusChanged(object? sender, bool isConnected)
    {
        IsConnected = isConnected;
        ConnectionStatusChanged?.Invoke(this, isConnected);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopAsync().GetAwaiter().GetResult();
        DetachClientEvents();
        _disposed = true;
    }
}

