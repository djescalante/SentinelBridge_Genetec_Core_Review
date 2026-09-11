using SentinelBridge.Core.Configuration;

namespace SentinelBridge.Core.Interfaces;

/// <summary>
/// Interface for secure configuration management
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Event raised when configuration changes
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;

    /// <summary>
    /// Get the current service configuration
    /// </summary>
    ServiceConfiguration GetConfiguration();

    /// <summary>
    /// Save configuration with encrypted sensitive values
    /// </summary>
    Task SaveConfigurationAsync(ServiceConfiguration configuration, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reload configuration from disk
    /// </summary>
    Task ReloadConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Encrypt a sensitive value using DPAPI
    /// </summary>
    string EncryptValue(string plainText);

    /// <summary>
    /// Decrypt a sensitive value using DPAPI
    /// </summary>
    string DecryptValue(string encryptedText);

    /// <summary>
    /// Validate configuration settings
    /// </summary>
    ConfigurationValidationResult ValidateConfiguration(ServiceConfiguration configuration);

    /// <summary>
    /// Start monitoring configuration file for changes
    /// </summary>
    void StartMonitoring();

    /// <summary>
    /// Stop monitoring configuration file for changes
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Reads and parses the config file from disk without
    /// changing the in-memory state or rewriting the file. Structural corruption (invalid
    /// JSON, missing <c>ServiceConfiguration</c> section, deserialize/decrypt failure) is
    /// reported via <see cref="ConfigurationLoadStatus.Corrupt"/>. Domain-validation
    /// failures are NOT reported here — a structurally valid file is <see cref="ConfigurationLoadStatus.Valid"/>
    /// and validation remains a separate step.
    /// </summary>
    ConfigurationLoadResult LoadConfigurationFromDisk();

    /// <summary>
    /// Persists the seed configuration modeled from the
    /// injected <c>IConfiguration</c>. The seed fails strict validation by design (e.g.
    /// empty credentials), so regeneration/restore MUST bypass validation. Sets the
    /// in-memory configuration to the persisted seed on success.
    /// </summary>
    Task RestoreSeedConfigurationAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Load status reported by the <see cref="IConfigurationManager.LoadConfigurationFromDisk"/> probe.
/// </summary>
public enum ConfigurationLoadStatus
{
    /// <summary>The file exists, parses, deserializes, and decrypts successfully.</summary>
    Valid,
    /// <summary>No file exists at the resolved configuration path.</summary>
    Missing,
    /// <summary>The file exists but is structurally corrupt (invalid JSON, missing section, decrypt/deserialize failure).</summary>
    Corrupt
}

/// <summary>
/// Result of the non-destructive disk probe. <see cref="Configuration"/> is non-null only
/// for <see cref="ConfigurationLoadStatus.Valid"/>; <see cref="Errors"/> lists the structural
/// problems for <see cref="ConfigurationLoadStatus.Corrupt"/>.
/// </summary>
public sealed record ConfigurationLoadResult(
    ConfigurationLoadStatus Status,
    ServiceConfiguration? Configuration,
    IReadOnlyList<string>? Errors);

/// <summary>
/// Event arguments for configuration changes
/// </summary>
public class ConfigurationChangedEventArgs(ServiceConfiguration oldConfig, ServiceConfiguration newConfig, List<string> changedSections) : EventArgs
{
    public ServiceConfiguration OldConfiguration { get; } = oldConfig;
    public ServiceConfiguration NewConfiguration { get; } = newConfig;
    public List<string> ChangedSections { get; } = changedSections;
}

/// <summary>
/// Configuration validation result
/// </summary>
public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public void AddError(string error) => Errors.Add(error);
    public void AddWarning(string warning) => Warnings.Add(warning);
}

