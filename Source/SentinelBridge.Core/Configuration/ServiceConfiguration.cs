using SentinelBridge.Core.Models;

namespace SentinelBridge.Core.Configuration;

public class ServiceConfiguration
{
    public GenetecConfiguration Genetec { get; set; } = new();
    public HexagonConfiguration Hexagon { get; set; } = new();
    public FilteringConfiguration Filtering { get; set; } = new();
    public RetryConfiguration Retry { get; set; } = new();
    public LoggingConfiguration Logging { get; set; } = new();
    public SecurityConfiguration Security { get; set; } = new();
    public bool DryRunMode { get; set; } = false;
}

public class GenetecConfiguration
{
    public GenetecSdkConfiguration Sdk { get; set; } = new();
    public List<string> EventTypes { get; set; } = [];
    public EntityPrefetchConfiguration EntityPrefetch { get; set; } = new();
}

public class GenetecSdkConfiguration
{
    public string DirectoryServer { get; set; } = string.Empty;
    public int Port { get; set; } = 5500;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ApplicationCertificatePath { get; set; } = string.Empty;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public class EntityPrefetchConfiguration
{
    public bool EnablePrefetch { get; set; } = true;
    public List<string> EntityTypes { get; set; } = ["Camera", "Door", "Zone", "Area"];
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromHours(1);
}

public class HexagonConfiguration
{
    public string BaseUrl { get; set; } = "https://hexagon.example.invalid";
    public string Endpoint { get; set; } = "/Interface/SalvarAlarme";
    public HexagonAuthMethod AuthMethod { get; set; } = HexagonAuthMethod.None;
    public string ApiKey { get; set; } = string.Empty;
    public string BearerToken { get; set; } = string.Empty;
    public CertificateConfiguration? ClientCertificate { get; set; }
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public bool EnforceTls12Plus { get; set; } = false;
}

public enum HexagonAuthMethod
{
    None,
    ApiKey,
    BearerToken,
    ClientCertificate,
    mTLS
}

public class CertificateConfiguration
{
    public string Thumbprint { get; set; } = string.Empty;
    public string StoreName { get; set; } = "My";
    public string StoreLocation { get; set; } = "LocalMachine";
}

public class FilteringConfiguration
{
    public List<string> AllowedEventTypes { get; set; } = [];
    public List<EventSeverity> AllowedSeverities { get; set; } = [];
    public List<string> AllowedAreas { get; set; } = [];
    public List<string> AllowedZones { get; set; } = [];
    public List<string> ExcludedEntityTypes { get; set; } = [];
    public Dictionary<string, string> CustomFilters { get; set; } = [];
}

public class RetryConfiguration
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);
    public double BackoffMultiplier { get; set; } = 2.0;
    public bool UseJitter { get; set; } = true;
}

public class LoggingConfiguration
{
    public string LogLevel { get; set; } = "Information";
    public string LogPath { get; set; } = @"C:\ProgramData\SentinelBridge\Logs";
    public string FileNameTemplate { get; set; } = "sentinelbridge-{Date}.json";
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024;
    public int RetainedFileCountLimit { get; set; } = 30;
    public bool StructuredLogging { get; set; } = true;
}

public class SecurityConfiguration
{
    public bool MaskSensitiveData { get; set; } = true;
    public List<string> SensitiveFields { get; set; } =
    [
        "Password", "ApiKey", "BearerToken", "Token"
    ];
    public string EncryptionScope { get; set; } = "LocalMachine";
}

