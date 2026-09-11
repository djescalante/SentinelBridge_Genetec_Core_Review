namespace SentinelBridge.Core.Models;

/// <summary>
/// Stable, versioned catalog exposed to configuration/UI. Technical names match
/// Security Center 5.12 EventType names; labels are presentation-only.
/// </summary>
public sealed record GenetecEventDefinition(
    string Id,
    string DisplayName,
    EventType Category,
    EventSeverity DefaultSeverity,
    string[] SourceEntityTypes)
{
    public override string ToString() => $"{Category} — {DisplayName}";
}

public static class GenetecEventCatalog
{
    public const string Version = "5.12";

    private static readonly IReadOnlyList<GenetecEventDefinition> CuratedEvents =
    [
        new("AlarmTriggered", "Alarm triggered", EventType.Alarm, EventSeverity.High, ["Alarm"]),
        new("AlarmAcknowledged", "Alarm acknowledged", EventType.Alarm, EventSeverity.Medium, ["Alarm"]),
        new("AlarmInvestigating", "Alarm being investigated", EventType.Alarm, EventSeverity.Medium, ["Alarm"]),
        new("AlarmSourceConditionCleared", "Alarm condition cleared", EventType.Alarm, EventSeverity.Low, ["Alarm"]),

        new("AccessGranted", "Access granted", EventType.AccessControl, EventSeverity.Low, ["Door", "Cardholder", "Elevator"]),
        new("AccessDenied", "Access denied", EventType.AccessControl, EventSeverity.Medium, ["Door", "Cardholder", "Credential", "Elevator"]),
        new("AccessPointCredentialUnknown", "Access denied: unknown credential", EventType.AccessControl, EventSeverity.Medium, ["Door", "Elevator"]),
        new("AccessPointCredentialExpired", "Access denied: expired credential", EventType.AccessControl, EventSeverity.Medium, ["Door", "Elevator"]),
        new("AccessPointCredentialStolen", "Access denied: stolen credential", EventType.AccessControl, EventSeverity.High, ["Door", "Elevator"]),
        new("AccessPointDeniedByInvalidPIN", "Access denied: invalid PIN", EventType.AccessControl, EventSeverity.High, ["Door", "Elevator"]),
        new("AccessPointDeniedByAntipassback", "Access denied: antipassback", EventType.AccessControl, EventSeverity.Medium, ["Door", "Elevator"]),
        new("DuressPinEntered", "Duress PIN entered", EventType.AccessControl, EventSeverity.Critical, ["Door", "Elevator"]),
        new("DoorEntryDetected", "Entry detected", EventType.AccessControl, EventSeverity.Low, ["Door", "Cardholder"]),
        new("DoorNoEntryDetected", "No entry detected", EventType.AccessControl, EventSeverity.Medium, ["Door", "Cardholder"]),
        new("RequestToExit", "Request to exit", EventType.AccessControl, EventSeverity.Low, ["Door"]),

        new("DoorOpened", "Door opened", EventType.AccessControl, EventSeverity.Low, ["Door"]),
        new("DoorClosed", "Door closed", EventType.AccessControl, EventSeverity.Low, ["Door"]),
        new("DoorOpenedForTooLong", "Door opened too long", EventType.AccessControl, EventSeverity.High, ["Door"]),
        new("DoorOpenedWhenLocked", "Door opened while locked", EventType.AccessControl, EventSeverity.Critical, ["Door"]),
        new("DoorSecured", "Door secured", EventType.AccessControl, EventSeverity.Low, ["Door"]),
        new("DoorUnsecured", "Door unsecured", EventType.AccessControl, EventSeverity.Medium, ["Door"]),
        new("DoorManuallyUnlocked", "Door manually unlocked", EventType.AccessControl, EventSeverity.Medium, ["Door"]),
        new("DoorWarningUnitOffline", "Door unit offline", EventType.Health, EventSeverity.High, ["Door"]),

        new("CameraMotionOn", "Motion on", EventType.Video, EventSeverity.Medium, ["Camera"]),
        new("CameraMotionOff", "Motion off", EventType.Video, EventSeverity.Low, ["Camera"]),
        new("CameraSignalLost", "Video signal lost", EventType.Video, EventSeverity.High, ["Camera"]),
        new("CameraSignalRecovered", "Video signal recovered", EventType.Video, EventSeverity.Low, ["Camera"]),
        new("CameraTransmissionLost", "Camera transmission lost", EventType.Video, EventSeverity.High, ["Camera"]),
        new("CameraTransmissionRecovered", "Camera transmission recovered", EventType.Video, EventSeverity.Low, ["Camera"]),
        new("CameraNotArchiving", "Camera not archiving", EventType.Video, EventSeverity.High, ["Camera"]),
        new("CameraRtpPacketsLost", "RTP packets lost", EventType.Video, EventSeverity.Medium, ["Camera"]),
        new("ArchivingStartedOnMotion", "Recording started by motion", EventType.Video, EventSeverity.Low, ["Camera"]),
        new("ArchivingStoppedOnMotion", "Recording stopped after motion", EventType.Video, EventSeverity.Low, ["Camera"]),

        new("VideoAnalyticsFaceDetected", "Face detected", EventType.Video, EventSeverity.Medium, ["Camera"]),
        new("VideoAnalyticsFaceRecognized", "Face recognized", EventType.Video, EventSeverity.High, ["Camera"]),
        new("VideoAnalyticsObjectCrossedLine", "Object crossed line", EventType.Video, EventSeverity.Medium, ["Camera"]),
        new("VideoAnalyticsObjectLoitering", "Object loitering", EventType.Video, EventSeverity.High, ["Camera"]),
        new("VideoAnalyticsObjectFall", "Person falling", EventType.Video, EventSeverity.Critical, ["Camera"]),
        new("VideoAnalyticsTailgating", "Tailgating detected", EventType.Video, EventSeverity.High, ["Camera"]),
        new("VideoAnalyticsTampering", "Video tampering", EventType.Video, EventSeverity.High, ["Camera"]),

        new("InputAlarmActive", "Input alarm activated", EventType.Alarm, EventSeverity.High, ["Input"]),
        new("InputAlarmRestored", "Input alarm restored", EventType.Alarm, EventSeverity.Low, ["Input"]),
        new("InputBypassed", "Input bypassed", EventType.Health, EventSeverity.Medium, ["Input"]),
        new("InputTrouble", "Input trouble", EventType.Health, EventSeverity.High, ["Input"]),
        new("IntrusionAreaAlarmActivated", "Intrusion alarm activated", EventType.Alarm, EventSeverity.Critical, ["IntrusionArea"]),
        new("IntrusionAreaDisarmed", "Intrusion area disarmed", EventType.AccessControl, EventSeverity.Medium, ["IntrusionArea"]),
        new("ZoneArmed", "Zone armed", EventType.AccessControl, EventSeverity.Low, ["Zone"]),
        new("ZoneDisarmed", "Zone disarmed", EventType.AccessControl, EventSeverity.Medium, ["Zone"]),
        new("ZoneGlassbreak", "Glass break", EventType.Alarm, EventSeverity.Critical, ["Zone"]),

        new("HealthMonitoringEntityOffline", "Entity offline", EventType.Health, EventSeverity.High, ["Any"]),
        new("HealthMonitoringEntityOnline", "Entity online", EventType.Health, EventSeverity.Low, ["Any"]),
        new("HealthMonitoringCameraConnectionStoppedUnexpectedly", "Camera connection lost", EventType.Health, EventSeverity.High, ["Camera"]),
        new("HealthMonitoringCameraConnectionEstablished", "Camera connection restored", EventType.Health, EventSeverity.Low, ["Camera"]),
        new("HealthMonitoringRoleStoppedUnexpectedly", "Role stopped unexpectedly", EventType.Health, EventSeverity.Critical, ["Role"]),
        new("HealthMonitoringDatabaseLost", "Database connection lost", EventType.Health, EventSeverity.Critical, ["Role"]),
        new("HealthMonitoringDatabaseRecovered", "Database connection recovered", EventType.Health, EventSeverity.Low, ["Role"]),
        new("HealthMonitoringLowArchiveSpace", "Archive space low", EventType.Health, EventSeverity.High, ["Archiver"]),
        new("HealthMonitoringUnitConnectionFailed", "Unit connection failed", EventType.Health, EventSeverity.High, ["Unit"]),
        new("HealthMonitoringUnitConnectionRestored", "Unit connection restored", EventType.Health, EventSeverity.Low, ["Unit"]),

        new("LprRead", "License plate read", EventType.Custom, EventSeverity.Low, ["LprUnit"]),
        new("LprHit", "License plate hit", EventType.Alarm, EventSeverity.High, ["LprUnit", "Hotlist"]),
        new("LprNoMatch", "License plate no match", EventType.Custom, EventSeverity.Low, ["LprUnit"]),
        new("CustomEvent", "Custom event", EventType.Custom, EventSeverity.Medium, ["Any"])
    ];

    private static readonly HashSet<string> ExactSdkIds =
        new(GenetecSdkEventTypeIds.All, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<GenetecEventDefinition> Events { get; } = BuildEvents();

    private static readonly IReadOnlyDictionary<string, GenetecEventDefinition> ById =
        Events.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string[]> LegacyAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["AlarmSourceConditionCleared"] = ["AlarmConditionCleared"],
            ["AccessDenied"] = ["AccessRefused"],
            ["AccessPointCredentialUnknown"] = ["AccessUnknownCredential"],
            ["AccessPointCredentialExpired"] = ["AccessExpiredCredential"],
            ["AccessPointCredentialStolen"] = ["AccessStolenCredential"],
            ["AccessPointDeniedByInvalidPIN"] = ["AccessInvalidPin"],
            ["AccessPointDeniedByAntipassback"] = ["AccessAntipassbackViolation"],
            ["DuressPinEntered"] = ["AreaDuressPinEntered", "CardholderDuressPinEntered"],
            ["RequestToExit"] = ["DoorRexOn"],
            ["DoorOpened"] = ["DoorOpen"],
            ["DoorClosed"] = ["DoorClose"],
            ["DoorOpenedWhenLocked"] = ["DoorOpenWhileLockSecure"],
            ["ArchivingStartedOnMotion"] = ["ArchivingStartedOnMotionEvent"],
            ["ArchivingStoppedOnMotion"] = ["ArchivingStoppedOnMotionEvent"],
            ["InputTrouble"] = ["InputTroubleOpen", "InputTroubleShort", "InputStateTrouble"],
            ["HealthMonitoringEntityOffline"] = ["HealthMonitoringEventEntityOffline"],
            ["HealthMonitoringEntityOnline"] = ["HealthMonitoringEventEntityOnline"],
            ["HealthMonitoringCameraConnectionStoppedUnexpectedly"] = ["HealthMonitoringEventCameraConnectionStoppedUnexpectedly"],
            ["HealthMonitoringCameraConnectionEstablished"] = ["HealthMonitoringEventCameraConnectionEstablished"],
            ["HealthMonitoringRoleStoppedUnexpectedly"] = ["HealthMonitoringEventRoleStoppedUnexpectedly"],
            ["HealthMonitoringDatabaseLost"] = ["HealthMonitoringEventDatabaseLost"],
            ["HealthMonitoringDatabaseRecovered"] = ["HealthMonitoringEventDatabaseRecovered"],
            ["HealthMonitoringLowArchiveSpace"] = ["HealthMonitoringEventLowArchiveSpace"],
            ["HealthMonitoringUnitConnectionFailed"] = ["HealthMonitoringEventUnitConnectionFailed"],
            ["HealthMonitoringUnitConnectionRestored"] = ["HealthMonitoringEventUnitConnectionRestored"]
        };

    private static IReadOnlyList<GenetecEventDefinition> BuildEvents()
    {
        var curated = CuratedEvents
            .Where(e => ExactSdkIds.Contains(e.Id))
            .ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

        return GenetecSdkEventTypeIds.All
            .Where(id => !id.Equals("None", StringComparison.OrdinalIgnoreCase))
            .Select(id => curated.TryGetValue(id, out var definition)
                ? definition with { Id = id, DisplayName = GenetecEventSpanishLocalizer.Translate(id) }
                : CreateFallbackDefinition(id))
            .OrderBy(e => e.Category)
            .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static GenetecEventDefinition CreateFallbackDefinition(string id)
    {
        var category = Classify(id);
        return new GenetecEventDefinition(
            id,
            GenetecEventSpanishLocalizer.Translate(id),
            category,
            InferSeverity(id),
            [category switch
            {
                EventType.AccessControl => "Access control entity",
                EventType.Video => "Video entity",
                EventType.Alarm => "Alarm source",
                EventType.Health => "System entity",
                _ => "Any"
            }]);
    }

    private static EventType Classify(string id)
    {
        if (id.Contains("HealthMonitoring", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("Warning", StringComparison.OrdinalIgnoreCase))
            return EventType.Health;

        if (ContainsAny(id, "Access", "Cardholder", "Credential", "Door", "Elevator", "Zone", "Antipassback", "Occupancy"))
            return EventType.AccessControl;

        if (ContainsAny(id, "Alarm", "Intrusion", "Duress", "Tamper", "InputTrouble", "GlassBreak", "HotlistHit"))
            return EventType.Alarm;

        if (ContainsAny(id, "Camera", "Video", "Archiver", "Archive", "Recording", "Motion", "Media", "Stream", "Fusion"))
            return EventType.Video;

        return EventType.Custom;
    }

    private static EventSeverity InferSeverity(string id)
    {
        if (ContainsAny(id, "Critical", "Duress", "ForcedOpen")) return EventSeverity.Critical;
        if (ContainsAny(id, "Failure", "Failed", "Error", "Tamper", "Alarm", "Lost", "Offline", "Stolen",
                "Expired", "Refused", "Denied", "Invalid", "Unexpected", "Abnormal", "DiskFull"))
            return EventSeverity.High;
        if (ContainsAny(id, "Recovered", "Restored", "Normal", "Connected", "Online", "Success", "Succeeded",
                "Completed", "Acknowledged", "Closed", "Close", "Disarmed"))
            return EventSeverity.Low;
        return EventSeverity.Medium;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    public static GenetecEventDefinition? Find(string sdkEventName)
    {
        if (ById.TryGetValue(sdkEventName, out var direct)) return direct;
        var normalized = sdkEventName.EndsWith("Event", StringComparison.OrdinalIgnoreCase)
            ? sdkEventName[..^5]
            : sdkEventName;
        return ById.TryGetValue(normalized, out var value) ? value : null;
    }

    public static HashSet<string> ExpandSelection(IEnumerable<string>? configuredValues)
    {
        var values = configuredValues?.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray() ?? [];
        if (values.Length == 0) return [];

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (Enum.TryParse<EventType>(value, true, out var legacyCategory))
            {
                foreach (var definition in Events.Where(e => e.Category == legacyCategory)) result.Add(definition.Id);
            }
            else if (LegacyAliases.TryGetValue(value, out var aliases))
            {
                result.UnionWith(aliases);
            }
            else
            {
                result.Add(value);
            }
        }
        return result;
    }
}

