using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SentinelBridge.Core.Configuration;
using SentinelBridge.Core.Interfaces;
using SentinelBridge.Core.Services;

namespace SentinelBridge.Service.Tests.Connectors.Genetec;

/// <summary>
/// Test utilities for Genetec SDK integration tests
/// </summary>
public static class TestUtilities
{
    private static readonly Lazy<string> TestCertificatePath = new(CreateTestCertificateFile);

    /// <summary>
    /// Creates a mock configuration manager for testing
    /// </summary>
    public static IConfigurationManager CreateMockConfigurationManager()
    {
        return new MockConfigurationManager();
    }

    /// <summary>
    /// Creates valid Genetec SDK configuration options for testing
    /// </summary>
    public static IOptions<GenetecSdkConfiguration> CreateValidGenetecSdkOptions()
    {
        var config = new GenetecSdkConfiguration
        {
            DirectoryServer = "localhost",
            Port = 5500,
            Username = "<TEST_USERNAME>",
            Password = "<TEST_PASSWORD>",
            ApplicationCertificatePath = TestCertificatePath.Value,
            ConnectionTimeout = TimeSpan.FromSeconds(30)
        };

        return Options.Create(config);
    }

    /// <summary>
    /// Creates a null logger for testing
    /// </summary>
    public static ILogger<T> CreateNullLogger<T>()
    {
        return NullLogger<T>.Instance;
    }

    /// <summary>
    /// Checks if Genetec Platform SDK is installed on the test machine
    /// </summary>
    public static bool IsPlatformSdkInstalled =>
        GenetecSdkDiagnostics.PerformDiagnostics().OverallStatus == SdkStatus.Available;

    public static string GetTestApplicationCertificatePath() => TestCertificatePath.Value;

    private static string CreateTestCertificateFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "sentinelbridge-genetec-sdk-test-certificate.xml");
        File.WriteAllText(path, "<Certificate><ApplicationId>TEST-APPLICATION-ID</ApplicationId></Certificate>");
        return path;
    }
}

/// <summary>
/// Mock implementation of IConfigurationManager for testing
/// </summary>
internal class MockConfigurationManager : IConfigurationManager
{
    private readonly ServiceConfiguration _configuration = new();

#pragma warning disable CS0067 // Event is never used - Mock implementation for testing interface compliance
    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
#pragma warning restore CS0067

    public ServiceConfiguration GetConfiguration()
    {
        return _configuration;
    }

    public Task SaveConfigurationAsync(ServiceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ReloadConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string EncryptValue(string plainText)
    {
        // For testing, just return the value as-is (simulating encryption)
        return plainText;
    }

    public string DecryptValue(string encryptedText)
    {
        // For testing, just return the value as-is (simulating decryption)
        return encryptedText;
    }

    public ConfigurationValidationResult ValidateConfiguration(ServiceConfiguration configuration)
    {
        return new ConfigurationValidationResult { IsValid = true };
    }

    public void StartMonitoring()
    {
        // No-op for testing
    }

    public void StopMonitoring()
    {
        // No-op for testing
    }

    public ConfigurationLoadResult LoadConfigurationFromDisk()
    {
        // Mock: the in-memory configuration the test configured is always "valid".
        return new ConfigurationLoadResult(ConfigurationLoadStatus.Valid, _configuration, null);
    }

    public Task RestoreSeedConfigurationAsync(CancellationToken cancellationToken = default)
    {
        // No-op for testing
        return Task.CompletedTask;
    }
}
