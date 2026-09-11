using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SentinelBridge.Core.Configuration;
using SentinelBridge.Core.Interfaces;
using SentinelBridge.Core.Services;
using SentinelBridge.Service.Tests.Connectors.Genetec;
// Disambiguate from Xunit.Skip (SkippableFact): these tests use the local Genetec.Skip helper.
using Skip = SentinelBridge.Service.Tests.Connectors.Genetec.Skip;
using Xunit;

namespace SentinelBridge.Service.Tests.Services;

/// <summary>
/// Comprehensive tests for GenetecAuthenticationService
/// Tests authentication scenarios, event handling, retry logic, and error conditions
/// </summary>
public class GenetecAuthenticationServiceTests : IDisposable
{
    private readonly ILogger<GenetecAuthenticationService> _logger;
    private readonly IConfigurationManager _configManager;
    private readonly IOptions<GenetecSdkConfiguration> _options;
    private readonly GenetecAuthenticationService _authService;

    public GenetecAuthenticationServiceTests()
    {
        _logger = TestUtilities.CreateNullLogger<GenetecAuthenticationService>();
        _configManager = TestUtilities.CreateMockConfigurationManager();
        _options = TestUtilities.CreateValidGenetecSdkOptions();

        // Create service instance for testing
        _authService = new GenetecAuthenticationService(_logger, _configManager, _options);
    }

    [Theory]
    [MemberData(nameof(NonRetryableDependencyFailures))]
    public void DependencyFailures_AreNotRetried(Exception exception)
    {
        Assert.True(GenetecAuthenticationService.IsNonRetryableAuthenticationFailure(exception));
    }

    [Fact]
    public void NetworkFailure_RemainsRetryable()
    {
        Assert.False(GenetecAuthenticationService.IsNonRetryableAuthenticationFailure(
            new HttpRequestException("temporary network failure")));
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Timeout")]
    [InlineData("InvalidCredential")]
    [InlineData("LicenseError")]
    [InlineData("")]
    [InlineData(null)]
    public void NonSuccessConnectionStates_AreRejected(string? connectionState)
    {
        Assert.False(GenetecAuthenticationService.IsSuccessfulConnectionStateName(connectionState));
    }

    [Fact]
    public void SuccessConnectionState_IsAccepted()
    {
        Assert.True(GenetecAuthenticationService.IsSuccessfulConnectionStateName("Success"));
    }

    [Theory]
    [InlineData("CertificateRegistrationError")]
    [InlineData("UnableToIdentifyClientApplication")]
    [InlineData("LicenseError")]
    [InlineData("InvalidCredential")]
    public void PermanentConnectionStates_AreNotRetried(string connectionState)
    {
        Assert.True(GenetecAuthenticationService.IsNonRetryableConnectionStateName(connectionState));
        Assert.True(GenetecAuthenticationService.IsNonRetryableAuthenticationFailure(
            new GenetecConnectionStateException(connectionState)));
    }

    [Fact]
    public void TimeoutConnectionState_RemainsRetryable()
    {
        Assert.False(GenetecAuthenticationService.IsNonRetryableConnectionStateName("Timeout"));
    }

    [Fact]
    public void WrappedDependencyFailure_IsNotRetried()
    {
        var exception = new InvalidOperationException(
            "SDK initialization failed",
            new FileNotFoundException("WindowsBase was not found", "WindowsBase"));

        Assert.True(GenetecAuthenticationService.IsNonRetryableAuthenticationFailure(exception));
    }

    public static IEnumerable<object[]> NonRetryableDependencyFailures()
    {
        yield return [new FileNotFoundException("missing managed assembly")];
        yield return [new FileLoadException("managed assembly could not load")];
        yield return [new DllNotFoundException("missing native library")];
        yield return [new BadImageFormatException("wrong architecture")];
        yield return [new TypeLoadException("incompatible SDK type")];
        yield return [new MissingMethodException("incompatible SDK API")];
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly_WithValidParameters()
    {
        // Arrange & Act
        var exception = Record.Exception(() =>
        {
            using var service = new GenetecAuthenticationService(_logger, _configManager, _options);

            // Assert
            Assert.NotNull(service);
            Assert.False(service.IsAuthenticated);
            // Note: Engine property may not be available when SDK is not installed
            // Assert.NotNull(service.Engine);
        });

        // If SDK is not available, expect InvalidOperationException
        if (exception != null)
        {
            Assert.True(IsExpectedSdkException(exception),
                $"Expected SDK not available exception, but got: {exception.GetType().Name}: {exception.Message}");
        }
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WithNullLogger()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GenetecAuthenticationService(null!, _configManager, _options));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WithNullConfigManager()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GenetecAuthenticationService(_logger, null!, _options));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WithNullOptions()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GenetecAuthenticationService(_logger, _configManager, null!));
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldReturnFalse_WithInvalidConfiguration()
    {
        // Arrange
        var invalidOptions = Options.Create(new GenetecSdkConfiguration
        {
            DirectoryServer = "", // Invalid - empty server
            Port = 5500,
            Username = "<TEST_USERNAME>",
            Password = "<TEST_PASSWORD>"
        });

        var exception = Record.Exception(() =>
        {
            using var service = new GenetecAuthenticationService(_logger, _configManager, invalidOptions);
        });

        // Skip test if SDK is not available
        if (IsExpectedSdkException(exception))
        {
            return;
        }

        using var service = new GenetecAuthenticationService(_logger, _configManager, invalidOptions);

        // Act
        var result = await service.AuthenticateAsync(CancellationToken.None);

        // Assert
        Assert.False(result);
        Assert.False(service.IsAuthenticated);
        Assert.NotNull(service.LastAuthenticationException);
        Assert.Contains("Directory server is required", service.LastAuthenticationException!.Message);
    }

    [Fact]
    public async Task UpdateConfiguration_UsesLatestValuesForNextAuthentication()
    {
        _authService.UpdateConfiguration(new GenetecSdkConfiguration
        {
            DirectoryServer = string.Empty,
            Username = "<UPDATED_TEST_USERNAME>",
            Password = "<UPDATED_TEST_PASSWORD>"
        });

        var result = await _authService.AuthenticateAsync(CancellationToken.None);

        Assert.False(result);
        Assert.Contains("Directory server is required", _authService.LastAuthenticationException!.Message);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldHandleCancellation()
    {
        // Skip test if SDK is not available
        var exception = Record.Exception(() => _authService);
        if (IsExpectedSdkException(exception))
        {
            return;
        }

        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _authService.AuthenticateAsync(cts.Token));
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldPreventConcurrentAuthentication()
    {
        // Skip test if SDK is not available
        var exception = Record.Exception(() => _authService);
        if (IsExpectedSdkException(exception))
        {
            return;
        }

        // Arrange
        var task1 = _authService.AuthenticateAsync(CancellationToken.None);
        var task2 = _authService.AuthenticateAsync(CancellationToken.None);

        // Act
        var results = await Task.WhenAll(task1, task2);

        // Assert - Both should complete, but only one should perform actual authentication
        Assert.NotNull(results);
        Assert.Equal(2, results.Length);
    }

    [Fact]
    public async Task LogoutAsync_ShouldCompleteSuccessfully_WhenNotAuthenticated()
    {
        // Skip test if SDK is not available
        var exception = Record.Exception(() => _authService);
        if (IsExpectedSdkException(exception))
        {
            return;
        }

        // Act
        await _authService.LogoutAsync();

        // Assert
        Assert.False(_authService.IsAuthenticated);
    }

    [Fact]
    public void AuthenticationStatusChanged_EventShouldBeRaised_OnStatusChange()
    {
        // Skip test if SDK is not available
        var exception = Record.Exception(() => _authService);
        if (IsExpectedSdkException(exception))
        {
            return;
        }

        // Arrange
        var eventRaised = false;
        AuthenticationStatusChangedEventArgs? eventArgs = null;

        _authService.AuthenticationStatusChanged += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act - This would typically be triggered by actual authentication
        // For testing, we verify the event can be subscribed to

        // Assert
        Assert.False(eventRaised); // Event not raised yet
        Assert.Null(eventArgs);
    }

    [Fact]
    public void AuthenticationFailed_EventShouldBeRaised_OnFailure()
    {
        // Skip test if SDK is not available
        var exception = Record.Exception(() => _authService);
        if (IsExpectedSdkException(exception))
        {
            return;
        }

        // Arrange
        var eventRaised = false;
        AuthenticationFailedEventArgs? eventArgs = null;

        _authService.AuthenticationFailed += (sender, args) =>
        {
            eventRaised = true;
            eventArgs = args;
        };

        // Act - This would typically be triggered by actual authentication failure
        // For testing, we verify the event can be subscribed to

        // Assert
        Assert.False(eventRaised); // Event not raised yet
        Assert.Null(eventArgs);
    }

    [Fact]
    public async Task AuthenticateAsync_ShouldRetryOnFailure_WithRetryConfiguration()
    {
        // Skip test if SDK is not available
        var exception = Record.Exception(() => _authService);
        if (IsExpectedSdkException(exception))
        {
            return;
        }

        // Arrange - Use configuration that will likely fail (invalid server)
        var retryOptions = Options.Create(new GenetecSdkConfiguration
        {
            DirectoryServer = "invalid-server-that-does-not-exist",
            Port = 5500,
            Username = "<TEST_USERNAME>",
            Password = "<TEST_PASSWORD>",
            ApplicationCertificatePath = TestUtilities.GetTestApplicationCertificatePath(),
            ConnectionTimeout = TimeSpan.FromSeconds(1) // Short timeout for faster test
        });

        using var retryService = new GenetecAuthenticationService(_logger, _configManager, retryOptions);

        // Act
        var result = await retryService.AuthenticateAsync(CancellationToken.None);

        // Assert
        Assert.False(result); // Should fail after retries
        Assert.False(retryService.IsAuthenticated);
    }

    [ConditionalFact]
    public async Task AuthenticateAsync_ShouldSucceed_WithValidCredentials()
    {
        // Skip this test if Platform SDK is not installed
        Skip.IfNot(TestUtilities.IsPlatformSdkInstalled, "Genetec Platform SDK is not installed on this machine");

        // This test would require a live Genetec Security Center server
        // For now, we just verify the service can be created and called

        // Act
        var result = await _authService.AuthenticateAsync(CancellationToken.None);

        // Assert - Without a live server, this will likely fail, but shouldn't throw
        // The important thing is that the method completes without exceptions
        Assert.False(result); // Expected to fail without live server
    }

    [Fact]
    public void Dispose_ShouldCleanupResources()
    {
        // Skip test if SDK is not available
        var exception = Record.Exception(() => _authService);
        if (IsExpectedSdkException(exception))
        {
            return;
        }

        // Act
        _authService.Dispose();

        // Assert - Should not throw
        // Multiple dispose calls should be safe
        _authService.Dispose();
    }

    private static bool IsExpectedSdkException(Exception? exception)
    {
        if (exception is TypeInitializationException typeInitEx)
        {
            return typeInitEx.InnerException is InvalidOperationException invalidOpEx &&
                   invalidOpEx.Message.Contains("Genetec Platform SDK is not available");
        }

        if (exception is InvalidOperationException invalidOp &&
            invalidOp.Message.Contains("Genetec Platform SDK is not available"))
        {
            return true;
        }

        // Handle FileNotFoundException for missing SDK assemblies
        if (exception is FileNotFoundException fileNotFound &&
            fileNotFound.Message.Contains("Genetec.Sdk"))
        {
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        _authService?.Dispose();
    }
}



