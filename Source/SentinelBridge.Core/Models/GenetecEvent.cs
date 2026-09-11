namespace SentinelBridge.Core.Models;

/// <summary>
/// Represents a normalized event from Genetec Security Center
/// </summary>
public class GenetecEvent
{
    /// <summary>
    /// Unique identifier for the event from Genetec
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Stable, unique external alarm ID for idempotent delivery
    /// </summary>
    public string ExternalAlarmId { get; set; } = string.Empty;

    /// <summary>
    /// Type of event (Alarm, AccessControl, Video, Health, Custom)
    /// </summary>
    public EventType EventType { get; set; }

    /// <summary>Exact Security Center SDK event identifier.</summary>
    public int SdkEventTypeId { get; set; }
    public string SdkEventTypeName { get; set; } = string.Empty;
    public Guid SourceGuid { get; set; }
    public string? SdkGroupId { get; set; }
    public string? RawEventClass { get; set; }

    /// <summary>
    /// Event severity/priority level
    /// </summary>
    public EventSeverity Severity { get; set; }

    /// <summary>
    /// Timestamp when the event occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Source entity information (camera, door, zone, etc.)
    /// </summary>
    public EntityInfo? SourceEntity { get; set; }

    /// <summary>
    /// Area or zone where the event occurred
    /// </summary>
    public string? Area { get; set; }

    /// <summary>
    /// Zone identifier
    /// </summary>
    public string? Zone { get; set; }

    /// <summary>
    /// Event description or message
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Additional event-specific data
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = [];

    /// <summary>
    /// Processing metadata
    /// </summary>
    public ProcessingMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Event type enumeration
/// </summary>
public enum EventType
{
    Alarm,
    AccessControl,
    Video,
    Health,
    Custom
}

/// <summary>
/// Event severity levels
/// </summary>
public enum EventSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Information about the source entity
/// </summary>
public class EntityInfo
{
    public Guid EntityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Dictionary<string, string> Properties { get; set; } = [];
}

/// <summary>
/// Processing metadata for tracking and observability
/// </summary>
public class ProcessingMetadata
{
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public int RetryCount { get; set; } = 0;
    public DateTimeOffset? LastRetryAt { get; set; }
    public string? LastError { get; set; }
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
}

/// <summary>
/// Processing status enumeration
/// </summary>
public enum ProcessingStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Retrying
}

