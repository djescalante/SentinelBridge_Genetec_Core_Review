using SentinelBridge.Core.Models;

namespace SentinelBridge.Service.Tests.Services;

public class GenetecEventCatalogTests
{
    [Fact]
    public void Catalog_IsVersionedAndContainsPriorityFamilies()
    {
        Assert.Equal("5.12", GenetecEventCatalog.Version);
        Assert.Contains(GenetecEventCatalog.Events, e => e.Id == "AlarmTriggered" && e.Category == EventType.Alarm);
        Assert.Contains(GenetecEventCatalog.Events, e => e.Id == "AccessGranted" && e.Category == EventType.AccessControl);
        Assert.Contains(GenetecEventCatalog.Events, e => e.Id == "CameraSignalLost" && e.Category == EventType.Video);
        Assert.Contains(GenetecEventCatalog.Events, e => e.Id == "HealthMonitoringEventEntityOffline" && e.Category == EventType.Health);
        Assert.Equal(599, GenetecEventCatalog.Events.Count);
        Assert.DoesNotContain(GenetecEventCatalog.Events, e => e.Id == "None");
        Assert.Equal(GenetecEventCatalog.Events.Count,
            GenetecEventCatalog.Events.Select(e => e.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("AccessGranted", "AccessGranted")]
    [InlineData("AccessGrantedEvent", "AccessGranted")]
    [InlineData("CameraSignalLostEvent", "CameraSignalLost")]
    public void Find_AcceptsSdkNameAndEventClassSuffix(string sdkName, string expectedId)
    {
        Assert.Equal(expectedId, GenetecEventCatalog.Find(sdkName)?.Id);
    }

    [Fact]
    public void ExpandSelection_MigratesLegacyCategoriesAndPreservesExactIds()
    {
        var selected = GenetecEventCatalog.ExpandSelection(["Alarm", "CameraSignalLost"]);

        Assert.Contains("AlarmTriggered", selected);
        Assert.Contains("InputAlarmActive", selected);
        Assert.Contains("CameraSignalLost", selected);
        Assert.DoesNotContain("AccessGranted", selected);
    }

    [Fact]
    public void ExpandSelection_EmptyMeansNoEventsSelected()
    {
        Assert.Empty(GenetecEventCatalog.ExpandSelection([]));
    }

    [Fact]
    public void ExpandSelection_MigratesPreviouslyUsedNonSdkAliases()
    {
        var selected = GenetecEventCatalog.ExpandSelection([
            "AccessDenied", "DoorOpened", "HealthMonitoringEntityOffline"]);

        Assert.Contains("AccessRefused", selected);
        Assert.Contains("DoorOpen", selected);
        Assert.Contains("HealthMonitoringEventEntityOffline", selected);
        Assert.DoesNotContain("AccessDenied", selected);
    }

    [Theory]
    [InlineData("AlarmTriggered", "Alarma activada")]
    [InlineData("AccessRefused", "Acceso denegado")]
    [InlineData("CameraSignalLost", "Señal de cámara perdida")]
    [InlineData("HealthMonitoringEventConnectionFailed", "Monitoreo de salud: conexión fallida")]
    public void Catalog_UsesSpanishDisplayNames(string id, string expectedName)
    {
        Assert.Equal(expectedName, GenetecEventCatalog.Find(id)?.DisplayName);
    }

    [Fact]
    public void AlarmRelatedEvents_AreGroupedUnderAlarmCategory()
    {
        Assert.Equal(EventType.Alarm, GenetecEventCatalog.Find("ArchivingStartedByAlarm")?.Category);
        Assert.Equal(EventType.Alarm, GenetecEventCatalog.Find("ExternalSystemAlarmTriggered")?.Category);
    }
}



