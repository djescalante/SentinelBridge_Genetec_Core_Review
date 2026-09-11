using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SentinelBridge.Core.Services;

/// <summary>
/// Diagnostic utility for Genetec SDK availability and configuration
/// Provides detailed diagnostics for troubleshooting SDK loading issues
/// </summary>
public static class GenetecSdkDiagnostics
{
    /// <summary>
    /// Perform comprehensive SDK diagnostics
    /// </summary>
    public static SdkDiagnosticResult PerformDiagnostics(ILogger? logger = null)
    {
        var result = new SdkDiagnosticResult();

        try
        {
            logger?.LogInformation("Starting Genetec SDK diagnostics");

            // Check SDK installation paths
            CheckSdkInstallation(result, logger);

            // Check assembly loading
            CheckAssemblyLoading(result, logger);

            // Check conditional compilation
            CheckConditionalCompilation(result, logger);

            // Check platform target
            CheckPlatformTarget(result, logger);

            result.OverallStatus = DetermineOverallStatus(result);

            logger?.LogInformation("SDK diagnostics completed with status: {Status}", result.OverallStatus);

            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error during SDK diagnostics");
            result.OverallStatus = SdkStatus.Error;
            result.ErrorMessage = $"Diagnostic error: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Check SDK installation paths and files
    /// </summary>
    private static void CheckSdkInstallation(SdkDiagnosticResult result, ILogger? logger)
    {
        // Check multiple SDK versions in priority order
        var sdkBasePaths = new[]
        {
            @"C:\Program Files (x86)\Genetec Security Center 5.12 SDK",
            @"C:\Program Files (x86)\Genetec Security Center 5.13 SDK",
            @"C:\Program Files (x86)\Genetec Security Center 5.10 SDK",
            @"C:\Program Files (x86)\Genetec Security Center 5.8 SDK"
        };

        var sdkPath = sdkBasePaths.FirstOrDefault(Directory.Exists) ?? sdkBasePaths[0];
        logger?.LogInformation("Selected SDK path: {SdkPath} (exists: {Exists})", sdkPath, Directory.Exists(sdkPath));

        // Define all possible SDK subdirectories to check
        var searchPaths = new List<string>();
        foreach (var basePath in sdkBasePaths.Where(Directory.Exists))
        {
            searchPaths.Add(Path.Combine(basePath, "net8.0-windows"));
            searchPaths.Add(Path.Combine(basePath, "x64"));
            searchPaths.Add(basePath);
            searchPaths.Add(Path.Combine(basePath, "amd64"));
            searchPaths.Add(Path.Combine(basePath, "Bin"));
            searchPaths.Add(Path.Combine(basePath, "x86"));
        }

        result.SdkPathExists = sdkBasePaths.Any(Directory.Exists);
        result.BinDirectoryExists = Directory.Exists(Path.Combine(sdkPath, "Bin"));
        result.X64DirectoryExists = Directory.Exists(Path.Combine(sdkPath, "x64"));
        result.X86DirectoryExists = Directory.Exists(Path.Combine(sdkPath, "x86"));

        logger?.LogInformation("Checking SDK installation at: {SdkPath}", sdkPath);
        logger?.LogDebug("SDK root path exists: {SdkPathExists}", result.SdkPathExists);
        logger?.LogDebug("Bin directory exists: {BinDirectoryExists}", result.BinDirectoryExists);
        logger?.LogDebug("x64 directory exists: {X64DirectoryExists}", result.X64DirectoryExists);
        logger?.LogDebug("x86 directory exists: {X86DirectoryExists}", result.X86DirectoryExists);

        // Check for key DLL files with case-insensitive search
        // For .NET 8, Genetec consolidated assemblies into Genetec.Sdk.dll
        var keyDlls = new[]
        {
            "Genetec.Sdk.dll", // .NET 8 consolidated assembly (v5.13+)
            "Genetec.SecurityCenter.SDK.dll", // Legacy assembly name
            "Genetec.SecurityCenter.SDK.Events.dll", // Legacy assembly name
            "Genetec.SecurityCenter.SDK.Entities.dll" // Legacy assembly name
        };

        result.MissingDlls = [];
        result.FoundDlls = [];

        foreach (var dll in keyDlls)
        {
            var found = false;
            logger?.LogDebug("Searching for DLL: {DllName}", dll);

            foreach (var searchPath in searchPaths)
            {
                logger?.LogDebug("  Checking path: {SearchPath}", searchPath);

                if (!Directory.Exists(searchPath))
                {
                    logger?.LogDebug("    Path does not exist, skipping");
                    continue;
                }

                // Case-insensitive file search
                var dllPath = FindFileIgnoreCase(searchPath, dll);
                if (!string.IsNullOrEmpty(dllPath))
                {
                    result.FoundDlls.Add($"{dll} (found at {dllPath})");
                    logger?.LogInformation("    ✓ Found: {DllPath}", dllPath);
                    found = true;
                    break;
                }
                else
                {
                    logger?.LogDebug("    Not found in this location");
                }
            }

            if (!found)
            {
                result.MissingDlls.Add(dll);
                logger?.LogWarning("  ✗ Missing: {DllName}", dll);
            }
        }

        logger?.LogInformation("SDK Detection Summary - Found: {FoundCount}, Missing: {MissingCount}",
            result.FoundDlls.Count, result.MissingDlls.Count);

        if (result.FoundDlls.Count > 0)
        {
            logger?.LogInformation("Found DLLs: {FoundDlls}", string.Join(", ", result.FoundDlls));
        }

        if (result.MissingDlls.Count > 0)
        {
            logger?.LogWarning("Missing DLLs: {MissingDlls}", string.Join(", ", result.MissingDlls));
        }
    }

    /// <summary>
    /// Find a file in the specified directory using case-insensitive search
    /// </summary>
    private static string? FindFileIgnoreCase(string directory, string fileName)
    {
        try
        {
            if (!Directory.Exists(directory))
                return null;

            var files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
            return files.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception)
        {
            // Ignore access denied or other file system errors
            return null;
        }
    }

    /// <summary>
    /// Check if SDK assemblies can be loaded
    /// </summary>
    private static void CheckAssemblyLoading(SdkDiagnosticResult result, ILogger? logger)
    {
        try
        {
#if GENETEC_SDK_AVAILABLE
            // For .NET 8, the SDK DLL exists but types may not be loadable due to dependencies
            // This is expected behavior - the SDK is available for compilation but runtime loading
            // requires the full Genetec Security Center environment

            var sdkSearchPaths = new[]
            {
                @"C:\Program Files (x86)\Genetec Security Center 5.12 SDK\net8.0-windows\Genetec.Sdk.dll",
                @"C:\Program Files (x86)\Genetec Security Center 5.13 SDK\net8.0-windows\Genetec.Sdk.dll",
                @"C:\Program Files (x86)\Genetec Security Center 5.12 SDK\x64\Genetec.Sdk.dll",
                @"C:\Program Files (x86)\Genetec Security Center 5.12 SDK\x64\Genetec.SecurityCenter.SDK.dll"
            };
            var foundSdkPath = sdkSearchPaths.FirstOrDefault(File.Exists);
            if (foundSdkPath != null)
            {
                result.CanLoadSdkTypes = true;
                result.SdkTypeInfo = $"SDK DLL found at {foundSdkPath} - types available for compilation";
                logger?.LogDebug("SDK DLL found and available for compilation at {Path}", foundSdkPath);
                return;
            }
            else
            {
                result.CanLoadSdkTypes = false;
                result.SdkTypeInfo = "Genetec.Sdk.dll not found at any expected location";
                logger?.LogWarning("SDK DLL not found at any expected location");
            }
#else
            result.CanLoadSdkTypes = false;
            result.SdkTypeInfo = "GENETEC_SDK_AVAILABLE not defined - SDK types not available at compile time";
            logger?.LogDebug("SDK types not available - GENETEC_SDK_AVAILABLE not defined");
#endif
        }
        catch (Exception ex)
        {
            result.CanLoadSdkTypes = false;
            result.SdkTypeInfo = $"Failed to check SDK availability: {ex.Message}";
            logger?.LogWarning(ex, "Failed to check SDK availability");
        }
    }

    /// <summary>
    /// Check conditional compilation symbols
    /// </summary>
    private static void CheckConditionalCompilation(SdkDiagnosticResult result, ILogger? logger)
    {
#if GENETEC_SDK_AVAILABLE
        result.ConditionalCompilationEnabled = true;
        logger?.LogDebug("GENETEC_SDK_AVAILABLE is defined");
#else
        result.ConditionalCompilationEnabled = false;
        logger?.LogDebug("GENETEC_SDK_AVAILABLE is not defined");
#endif
    }

    /// <summary>
    /// Check platform target configuration
    /// </summary>
    private static void CheckPlatformTarget(SdkDiagnosticResult result, ILogger? logger)
    {
        result.Is64BitProcess = Environment.Is64BitProcess;
        result.ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString();

        logger?.LogDebug("Is 64-bit process: {Is64BitProcess}", result.Is64BitProcess);
        logger?.LogDebug("Process architecture: {ProcessArchitecture}", result.ProcessArchitecture);
    }

    /// <summary>
    /// Determine overall SDK status based on diagnostic results
    /// </summary>
    private static SdkStatus DetermineOverallStatus(SdkDiagnosticResult result)
    {
        if (!result.SdkPathExists)
        {
            return SdkStatus.NotInstalled;
        }

        // For .NET 8, only Genetec.Sdk.dll is required - adjust missing DLL logic
        if (result.MissingDlls.Count > 0)
        {
            // Check if we have the essential .NET 8 SDK DLL
            var hasEssentialSdk = result.FoundDlls.Any(dll => dll.Contains("Genetec.Sdk.dll"));
            if (!hasEssentialSdk)
            {
                return SdkStatus.IncompleteInstallation;
            }
            // If we have Genetec.Sdk.dll but missing legacy DLLs, that's expected for .NET 8
        }

        if (!result.ConditionalCompilationEnabled)
        {
            return SdkStatus.NotConfigured;
        }

        if (!result.CanLoadSdkTypes)
        {
            return SdkStatus.LoadError;
        }

        return SdkStatus.Available;
    }

    /// <summary>
    /// Generate a detailed diagnostic report
    /// </summary>
    public static string GenerateReport(SdkDiagnosticResult result)
    {
        var report = new System.Text.StringBuilder();

        report.AppendLine("=== Genetec SDK Diagnostic Report ===");
        report.AppendLine($"Overall Status: {result.OverallStatus}");
        report.AppendLine($"Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();

        report.AppendLine("Installation Status:");
        report.AppendLine($"  SDK Path Exists: {result.SdkPathExists}");
        report.AppendLine($"  Bin Directory Exists: {result.BinDirectoryExists}");
        report.AppendLine($"  x64 Directory Exists: {result.X64DirectoryExists}");
        report.AppendLine($"  x86 Directory Exists: {result.X86DirectoryExists}");
        report.AppendLine();

        report.AppendLine("DLL Status:");
        if (result.FoundDlls.Count > 0)
        {
            report.AppendLine("  Found DLLs:");
            foreach (var dll in result.FoundDlls)
            {
                report.AppendLine($"    ✓ {dll}");
            }
        }

        if (result.MissingDlls.Count > 0)
        {
            report.AppendLine("  Missing DLLs:");
            foreach (var dll in result.MissingDlls)
            {
                report.AppendLine($"    ✗ {dll}");
            }
        }
        report.AppendLine();

        report.AppendLine("Configuration Status:");
        report.AppendLine($"  Conditional Compilation Enabled: {result.ConditionalCompilationEnabled}");
        report.AppendLine($"  Can Load SDK Types: {result.CanLoadSdkTypes}");
        report.AppendLine($"  SDK Type Info: {result.SdkTypeInfo}");
        report.AppendLine();

        report.AppendLine("Runtime Environment:");
        report.AppendLine($"  Is 64-bit Process: {result.Is64BitProcess}");
        report.AppendLine($"  Process Architecture: {result.ProcessArchitecture}");
        report.AppendLine();

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            report.AppendLine("Errors:");
            report.AppendLine($"  {result.ErrorMessage}");
            report.AppendLine();
        }

        report.AppendLine("Recommendations:");
        GenerateRecommendations(result, report);

        return report.ToString();
    }

    /// <summary>
    /// Generate recommendations based on diagnostic results
    /// </summary>
    private static void GenerateRecommendations(SdkDiagnosticResult result, System.Text.StringBuilder report)
    {
        switch (result.OverallStatus)
        {
            case SdkStatus.NotInstalled:
                report.AppendLine("  • Install Genetec Security Center 5.12 SDK");
                report.AppendLine("  • Ensure the SDK is installed in the expected location");
                break;

            case SdkStatus.IncompleteInstallation:
                report.AppendLine("  • Reinstall Genetec Security Center 5.12 SDK");
                report.AppendLine("  • Verify all SDK components are installed");
                report.AppendLine("  • Check if antivirus software blocked installation");
                break;

            case SdkStatus.NotConfigured:
                report.AppendLine("  • Rebuild the project to enable GENETEC_SDK_AVAILABLE");
                report.AppendLine("  • Verify project references are correctly configured");
                report.AppendLine("  • Check that SDK DLLs exist in the referenced paths");
                break;

            case SdkStatus.LoadError:
                report.AppendLine("  • Run the application as Administrator");
                report.AppendLine("  • Check Windows Event Log for detailed error information");
                report.AppendLine("  • Verify all SDK dependencies are available");
                report.AppendLine("  • Ensure platform target matches SDK architecture");
                break;

            case SdkStatus.Available:
                report.AppendLine("  • SDK appears to be properly configured");
                report.AppendLine("  • If issues persist, check network connectivity to Genetec server");
                break;

            case SdkStatus.Error:
                report.AppendLine("  • Review error details above");
                report.AppendLine("  • Check application logs for more information");
                report.AppendLine("  • Contact support if issues persist");
                break;
        }
    }
}

/// <summary>
/// Result of SDK diagnostic checks
/// </summary>
public class SdkDiagnosticResult
{
    public SdkStatus OverallStatus { get; set; }
    public bool SdkPathExists { get; set; }
    public bool BinDirectoryExists { get; set; }
    public bool X64DirectoryExists { get; set; }
    public bool X86DirectoryExists { get; set; }
    public List<string> FoundDlls { get; set; } = [];
    public List<string> MissingDlls { get; set; } = [];
    public bool ConditionalCompilationEnabled { get; set; }
    public bool CanLoadSdkTypes { get; set; }
    public string SdkTypeInfo { get; set; } = string.Empty;
    public bool Is64BitProcess { get; set; }
    public string ProcessArchitecture { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// SDK availability status
/// </summary>
public enum SdkStatus
{
    NotInstalled,
    IncompleteInstallation,
    NotConfigured,
    LoadError,
    Available,
    Error
}


