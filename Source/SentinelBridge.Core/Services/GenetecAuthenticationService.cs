using System.Diagnostics;
using System.Runtime.CompilerServices;
#if GENETEC_SDK_AVAILABLE
using Genetec.Sdk;
#endif
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SentinelBridge.Core.Configuration;
using SentinelBridge.Core.Interfaces;

using System.Runtime.Versioning;

namespace SentinelBridge.Core.Services;

[SupportedOSPlatform("windows")]
public class GenetecAuthenticationService : IDisposable
{
    private readonly ILogger<GenetecAuthenticationService> _logger;
    private readonly IConfigurationManager _configurationManager;
    private GenetecSdkConfiguration _options;
    private readonly SemaphoreSlim _connectionSemaphore;

    // Single Engine instance per application (Genetec best practice)
#if GENETEC_SDK_AVAILABLE
    private static Engine? _sharedEngine;
#else
    private static object? _sharedEngine;
#endif
    private static readonly object _engineLock = new();

    private bool _disposed;
    private volatile bool _isConnected;
#if GENETEC_SDK_AVAILABLE
    private bool _engineEventsAttached;
#endif
    private int _retryCount;
    private const int MaxRetryAttempts = 3;
    private const int RetryDelayMs = 2000;

    public GenetecAuthenticationService(
        ILogger<GenetecAuthenticationService> logger,
        IConfigurationManager configurationManager,
        IOptions<GenetecSdkConfiguration> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));

        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _connectionSemaphore = new SemaphoreSlim(1, 1);
    }

    public event EventHandler<AuthenticationStatusChangedEventArgs>? AuthenticationStatusChanged;
    public event EventHandler<AuthenticationFailedEventArgs>? AuthenticationFailed;
    public bool IsAuthenticated => _isConnected;
    public Exception? LastAuthenticationException { get; private set; }

    internal void UpdateConfiguration(GenetecSdkConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (_isConnected)
        {
            throw new InvalidOperationException("Authentication configuration cannot change while connected.");
        }

        _options = configuration;
    }
#if GENETEC_SDK_AVAILABLE
    public Engine Engine
#else
    public object Engine
#endif
    {
        get
        {
            if (_sharedEngine == null)
            {
                lock (_engineLock)
                {
                    if (_sharedEngine == null)
                    {
#if GENETEC_SDK_AVAILABLE
                        _sharedEngine = new Engine();
                        _logger.LogInformation("Genetec SDK Engine created");
#else
                        throw new InvalidOperationException("Genetec SDK is not available for compilation");
#endif
                    }
                }
            }
#if GENETEC_SDK_AVAILABLE
            AttachEngineEvents(_sharedEngine);
#endif
            return _sharedEngine;
        }
    }

    public async Task<bool> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("AuthenticateAsync called, awaiting connection semaphore...");
        var semaphoreWait = Stopwatch.StartNew();
        await _connectionSemaphore.WaitAsync(cancellationToken);
        semaphoreWait.Stop();
        if (semaphoreWait.ElapsedMilliseconds > 100)
            _logger.LogWarning("Connection semaphore wait took {ElapsedMs}ms - possible contention", semaphoreWait.ElapsedMilliseconds);

        try
        {
            if (_isConnected)
            {
                _logger.LogDebug("Already authenticated with Security Center");
                return true;
            }

            _logger.LogInformation("Authenticating with Security Center as user: {Username} at {Server}:{Port}, timeout: {Timeout}",
                _options.Username, _options.DirectoryServer, _options.Port, _options.ConnectionTimeout);

            LastAuthenticationException = null;

            ValidateConfiguration();

            var decryptedPassword = _configurationManager.DecryptValue(_options.Password);
            if (string.IsNullOrEmpty(decryptedPassword))
            {
                _logger.LogError("Decrypted password is empty. Raw password starts with ENCRYPTED: {IsEncrypted}",
                    _options.Password?.StartsWith("ENCRYPTED:") ?? false);
                throw new GenetecAuthenticationException("Password is required for authentication");
            }
            var applicationCertificate = GenetecApplicationCertificateLoader.Load(
                _options.ApplicationCertificatePath);
            _logger.LogInformation(
                "Genetec SDK application certificate loaded from {CertificatePath} with fingerprint {Fingerprint}",
                applicationCertificate.SourcePath,
                applicationCertificate.Fingerprint);

            return await AuthenticateWithRetryAsync(
                decryptedPassword,
                applicationCertificate,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Authentication cancelled");
            throw;
        }
        catch (Exception ex)
        {
            LastAuthenticationException = ex;
            _logger.LogError(ex, "Authentication failed for user: {Username}. ExceptionType={ExType}, IsConnected={IsConnected}, RetryCount={RetryCount}",
                _options.Username, ex.GetType().Name, _isConnected, _retryCount);
            OnAuthenticationFailed(new AuthenticationFailedEventArgs(ex, _retryCount));
            return false;
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    public async Task LogoutAsync()
    {
        await _connectionSemaphore.WaitAsync();

        try
        {
            if (!_isConnected)
            {
                _logger.LogDebug("Already logged out from Security Center");
                return;
            }

            _logger.LogInformation("Logging out from Security Center");

#if GENETEC_SDK_AVAILABLE
            LogOffEngine();
#endif
            SetAuthenticationState(false, "Logged out");
            _logger.LogInformation("Logout completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            throw new GenetecAuthenticationException("Logout failed", ex);
        }
        finally
        {
            _connectionSemaphore.Release();
        }
    }

    private async Task<bool> AuthenticateWithRetryAsync(
        string password,
        GenetecApplicationCertificate applicationCertificate,
        CancellationToken cancellationToken)
    {
        var totalTimer = Stopwatch.StartNew();
        Exception? lastException = null;
        for (_retryCount = 0; _retryCount <= MaxRetryAttempts; _retryCount++)
        {
            var attemptNumber = _retryCount + 1;
            var attemptTimer = Stopwatch.StartNew();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.ConnectionTimeout);

            _logger.LogDebug("Auth attempt {Attempt}/{MaxAttempts} starting. Total elapsed: {TotalMs}ms",
                attemptNumber, MaxRetryAttempts + 1, totalTimer.ElapsedMilliseconds);

            try
            {
#if GENETEC_SDK_AVAILABLE
                _logger.LogInformation("Attempting authentication with server: {Server}:{Port}, user: {User}",
                    _options.DirectoryServer, _options.Port, _options.Username);

                Engine.ClientCertificate = applicationCertificate.ApplicationId;

                var connectionState = await Engine.LogOnAsync(
                    _options.DirectoryServer,
                    _options.Username,
                    password,
                    timeoutCts.Token);

                _logger.LogInformation(
                    "Genetec SDK logon completed with connection state {ConnectionState}",
                    connectionState);

                if (!IsSuccessfulConnectionStateName(connectionState.ToString()))
                {
                    throw new GenetecConnectionStateException(connectionState.ToString());
                }

                LastAuthenticationException = null;
                attemptTimer.Stop();
                _logger.LogInformation("Authentication completed successfully in {ElapsedMs}ms on attempt {Attempt}",
                    attemptTimer.ElapsedMilliseconds, attemptNumber);
                _retryCount = 0;
                SetAuthenticationState(true, "Authentication successful");
                return true;
#else
                // Fallback when SDK is not available at compile time
                throw new InvalidOperationException("Genetec SDK is not available for compilation");
#endif
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested)
            {
                attemptTimer.Stop();
                lastException = new TimeoutException(
                    $"Authentication attempt {attemptNumber} timed out after {_options.ConnectionTimeout}.", ex);
                _logger.LogWarning("Authentication attempt {Attempt} timed out after {ElapsedMs}ms (timeout: {Timeout})",
                    attemptNumber, attemptTimer.ElapsedMilliseconds, _options.ConnectionTimeout);
            }
            catch (Exception ex) when (IsNonRetryableAuthenticationFailure(ex))
            {
                attemptTimer.Stop();
                _logger.LogError(ex,
                    "Authentication failure {ExType} is not retryable",
                    ex.GetType().Name);
                throw;
            }
            catch (Exception ex) when (_retryCount < MaxRetryAttempts)
            {
                attemptTimer.Stop();
                lastException = ex;
                var nextDelay = RetryDelayMs * (_retryCount + 1);
                _logger.LogWarning(ex, "Authentication attempt {Attempt} failed after {ElapsedMs}ms ({ExType}), retrying in {Delay}ms",
                    attemptNumber, attemptTimer.ElapsedMilliseconds, ex.GetType().Name, nextDelay);
            }

            // Wait before retry (except on last attempt)
            if (_retryCount < MaxRetryAttempts)
            {
                await Task.Delay(RetryDelayMs * (_retryCount + 1), cancellationToken);
            }
        }

        totalTimer.Stop();
        _logger.LogError("All {MaxAttempts} authentication attempts exhausted in {TotalMs}ms",
            MaxRetryAttempts + 1, totalTimer.ElapsedMilliseconds);
        throw new GenetecAuthenticationException(
            $"Authentication failed after {MaxRetryAttempts + 1} attempts: {lastException?.Message ?? "unknown error"}",
            lastException ?? new InvalidOperationException("Authentication failed without an SDK error."));
    }

    internal static bool IsNonRetryableAuthenticationFailure(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is GenetecApplicationCertificateException ||
                current is GenetecConnectionStateException stateException &&
                IsNonRetryableConnectionStateName(stateException.ConnectionState) ||
                current is FileNotFoundException or FileLoadException or DllNotFoundException or
                BadImageFormatException or TypeLoadException or MissingMethodException)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsSuccessfulConnectionStateName(string? stateName) =>
        string.Equals(stateName, "Success", StringComparison.Ordinal);

    internal static bool IsNonRetryableConnectionStateName(string? stateName) =>
        stateName is "CertificateRegistrationError" or "UnableToIdentifyClientApplication" or
            "LicenseError" or "InvalidCredential" or "UserAccountDisabledOrLocked" or
            "InsufficientPrivileges" or "InvalidVersion";

#if GENETEC_SDK_AVAILABLE
    // Keep SDK type resolution out of LogoutAsync's JIT path. This lets a harmless
    // already-logged-out call complete even when the SDK is intentionally absent,
    // as it is in the unit-test environment.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LogOffEngine()
    {
        _sharedEngine?.LogOff();
    }

    private void AttachEngineEvents(Engine engine)
    {
        if (_engineEventsAttached)
        {
            return;
        }

        engine.LoggedOn += OnEngineLoggedOn;
        engine.LoggedOff += OnEngineLoggedOff;
        engine.LogonFailed += OnEngineLogonFailed;
        _engineEventsAttached = true;
    }

    private void DetachEngineEvents()
    {
        if (!_engineEventsAttached || _sharedEngine == null)
        {
            return;
        }

        _sharedEngine.LoggedOn -= OnEngineLoggedOn;
        _sharedEngine.LoggedOff -= OnEngineLoggedOff;
        _sharedEngine.LogonFailed -= OnEngineLogonFailed;
        _engineEventsAttached = false;
    }

    private void OnEngineLoggedOn(object? sender, LoggedOnEventArgs e)
    {
        _logger.LogInformation("Genetec SDK confirmed logon to server {Server}", e.ServerName);
    }

    private void OnEngineLoggedOff(object? sender, LoggedOffEventArgs e)
    {
        _logger.LogWarning("Genetec SDK reported that the authenticated session ended");
        SetAuthenticationState(false, "The authenticated Genetec session ended");
    }

    private void OnEngineLogonFailed(object? sender, LogonFailedEventArgs e)
    {
        _logger.LogWarning(
            "Genetec SDK reported logon failure {FailureCode} for server {Server}: {Message}",
            e.FailureCode,
            e.ServerName,
            e.FormattedErrorMessage);
        SetAuthenticationState(false, $"Genetec logon failed: {e.FailureCode}");
    }
#endif

    private void SetAuthenticationState(bool isAuthenticated, string message)
    {
        var changed = _isConnected != isAuthenticated;
        _isConnected = isAuthenticated;
        if (changed)
        {
            OnAuthenticationStatusChanged(new AuthenticationStatusChangedEventArgs(isAuthenticated, message));
        }
    }

    private void ValidateConfiguration()
    {
        _logger.LogDebug("Validating auth config: Server={Server}, Port={Port}, Username={HasUser}, Password={HasPass}, ApplicationCertificate={HasCert}",
            _options.DirectoryServer ?? "(null)", _options.Port,
            !string.IsNullOrEmpty(_options.Username), !string.IsNullOrEmpty(_options.Password),
            !string.IsNullOrEmpty(_options.ApplicationCertificatePath));

        if (string.IsNullOrEmpty(_options.Username))
            throw new GenetecAuthenticationException("Username is required for authentication");

        if (string.IsNullOrEmpty(_options.DirectoryServer))
            throw new GenetecAuthenticationException("Directory server is required for authentication");

        if (string.IsNullOrEmpty(_options.Password))
            throw new GenetecAuthenticationException("Password is required for authentication");
    }

    #region Event Raising Methods

    private void OnAuthenticationStatusChanged(AuthenticationStatusChangedEventArgs e)
    {
        AuthenticationStatusChanged?.Invoke(this, e);
    }

    private void OnAuthenticationFailed(AuthenticationFailedEventArgs e)
    {
        AuthenticationFailed?.Invoke(this, e);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            try
            {
                if (_isConnected)
                {
                    LogoutAsync().GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal logout");
            }

#if GENETEC_SDK_AVAILABLE
            DetachEngineEvents();
#endif
            _connectionSemaphore.Dispose();
            _disposed = true;
        }
    }

    // Static cleanup method for the shared Engine
    public static void DisposeSharedEngine()
    {
        lock (_engineLock)
        {
            if (_sharedEngine != null)
            {
                try
                {
#if GENETEC_SDK_AVAILABLE
                    _sharedEngine.Dispose();
#endif
                }
                catch (Exception)
                {
                    // Ignore disposal errors
                }
                finally
                {
                    _sharedEngine = null;
                }
            }
        }
    }

    #endregion
}

public class AuthenticationStatusChangedEventArgs : EventArgs
{
    public bool IsAuthenticated { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }

    public AuthenticationStatusChangedEventArgs(bool isAuthenticated, string message)
    {
        IsAuthenticated = isAuthenticated;
        Message = message;
        Timestamp = DateTime.UtcNow;
    }
}

public class AuthenticationFailedEventArgs : EventArgs
{
    public Exception Exception { get; }
    public int RetryAttempt { get; }
    public DateTime Timestamp { get; }

    public AuthenticationFailedEventArgs(Exception exception, int retryAttempt)
    {
        Exception = exception;
        RetryAttempt = retryAttempt;
        Timestamp = DateTime.UtcNow;
    }
}

