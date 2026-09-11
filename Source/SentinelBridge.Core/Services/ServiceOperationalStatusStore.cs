using SentinelBridge.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SentinelBridge.Core.Services;

/// <summary>
/// Mantiene el estado funcional en memoria y publica una instantanea JSON
/// atomica para la UI. El archivo tambien funciona como heartbeat del proceso.
/// </summary>
public sealed class ServiceOperationalStatusStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _sync = new();
    private ServiceOperationalStatus _current = new()
    {
        ProcessId = Environment.ProcessId
    };

    public ServiceOperationalStatusStore(string? statusPath = null)
    {
        StatusPath = statusPath ?? GetDefaultStatusPath();
    }

    public string StatusPath { get; }

    public ServiceOperationalStatus Current
    {
        get
        {
            lock (_sync) return _current;
        }
    }

    public static string GetDefaultStatusPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "SentinelBridge",
        "service-status.json");

    public void SetPhase(
        ServiceOperationalPhase phase,
        string message,
        bool? sdkLoaded = null,
        bool? authenticated = null,
        bool? subscribed = null,
        int? selectedEventCount = null,
        bool? readyToPublish = null,
        bool? dryRun = null,
        string? lastError = null)
    {
        Update(status => status with
        {
            Phase = phase,
            Message = message,
            SdkLoaded = sdkLoaded ?? status.SdkLoaded,
            Authenticated = authenticated ?? status.Authenticated,
            Subscribed = subscribed ?? status.Subscribed,
            SelectedEventCount = selectedEventCount ?? status.SelectedEventCount,
            ReadyToPublish = readyToPublish ?? status.ReadyToPublish,
            DryRun = dryRun ?? status.DryRun,
            LastError = lastError
        }, persist: true);
    }

    public void UpdateMetrics(
        long received,
        long processed,
        long published,
        long failed,
        bool eventReceived = false,
        bool eventPublished = false,
        bool persist = false)
    {
        var now = DateTimeOffset.UtcNow;
        Update(status => status with
        {
            EventsReceived = received,
            EventsProcessed = processed,
            EventsPublished = published,
            EventsFailed = failed,
            LastEventReceivedAtUtc = eventReceived ? now : status.LastEventReceivedAtUtc,
            LastEventPublishedAtUtc = eventPublished ? now : status.LastEventPublishedAtUtc
        }, persist);
    }

    public static ServiceOperationalStatus? TryRead(string? statusPath = null)
    {
        var path = statusPath ?? GetDefaultStatusPath();
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<ServiceOperationalStatus>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void Update(Func<ServiceOperationalStatus, ServiceOperationalStatus> update, bool persist)
    {
        lock (_sync)
        {
            _current = update(_current) with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                ProcessId = Environment.ProcessId
            };

            if (persist) WriteAtomically(_current);
        }
    }

    private void WriteAtomically(ServiceOperationalStatus status)
    {
        var directory = Path.GetDirectoryName(StatusPath)
            ?? throw new InvalidOperationException("La ruta del estado operativo no tiene directorio");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{StatusPath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(status, JsonOptions));
        File.Move(temporaryPath, StatusPath, overwrite: true);
    }
}


