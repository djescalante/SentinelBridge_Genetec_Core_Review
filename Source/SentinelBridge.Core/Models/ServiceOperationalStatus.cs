namespace SentinelBridge.Core.Models;

/// <summary>
/// Estado funcional del puente. Es independiente del estado del proceso que
/// informa el Service Control Manager de Windows.
/// </summary>
public enum ServiceOperationalPhase
{
    Unknown,
    Starting,
    LoadingSdk,
    Authenticating,
    Subscribing,
    Running,
    DryRun,
    Degraded,
    Faulted,
    Stopping,
    Stopped
}

/// <summary>
/// Instantanea que el servicio publica para que la UI pueda comprobar que la
/// autenticacion, la escucha y el pipeline de salida estan realmente activos.
/// </summary>
public sealed record ServiceOperationalStatus
{
    public ServiceOperationalPhase Phase { get; init; } = ServiceOperationalPhase.Unknown;
    public string Message { get; init; } = "Estado operativo no disponible";
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public int ProcessId { get; init; }
    public bool SdkLoaded { get; init; }
    public bool Authenticated { get; init; }
    public bool Subscribed { get; init; }
    public int SelectedEventCount { get; init; }
    public bool ReadyToPublish { get; init; }
    public bool DryRun { get; init; }
    public long EventsReceived { get; init; }
    public long EventsProcessed { get; init; }
    public long EventsPublished { get; init; }
    public long EventsFailed { get; init; }
    public DateTimeOffset? LastEventReceivedAtUtc { get; init; }
    public DateTimeOffset? LastEventPublishedAtUtc { get; init; }
    public string? LastError { get; init; }
}


