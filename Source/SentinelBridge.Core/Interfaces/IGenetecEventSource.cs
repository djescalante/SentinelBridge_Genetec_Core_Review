using SentinelBridge.Core.Models;

namespace SentinelBridge.Core.Interfaces;

/// <summary>
/// Receives events from the Genetec Platform SDK.
/// </summary>
public interface IGenetecEventSource : IDisposable
{
    /// <summary>
    /// Event raised when a new event is received from Genetec
    /// </summary>
    event EventHandler<GenetecEvent> EventReceived;

    /// <summary>
    /// Event raised when an error occurs
    /// </summary>
    event EventHandler<Exception> ErrorOccurred;

    /// <summary>
    /// Event raised when connection status changes
    /// </summary>
    event EventHandler<bool> ConnectionStatusChanged;

    /// <summary>
    /// Initialize the event source
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start receiving events
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop receiving events
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connectivity to Genetec
    /// </summary>
    Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current connection status
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Indicates whether the SDK event handler is registered while the session is authenticated.
    /// </summary>
    bool IsSubscribed { get; }

    /// <summary>
    /// Prefetch entities for event enrichment
    /// </summary>
    Task PrefetchEntitiesAsync(CancellationToken cancellationToken = default);
}

