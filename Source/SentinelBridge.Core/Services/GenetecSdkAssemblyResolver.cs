using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace SentinelBridge.Core.Services;

/// <summary>
/// Resuelve las dependencias administradas y nativas desde la instalacion local
/// del SDK de Genetec. El MSI de SentinelBridge no redistribuye esos binarios.
/// </summary>
public static class GenetecSdkAssemblyResolver
{
    private const string SdkPathEnvironmentVariable = "GENETEC_SDK_PATH";
    private static string? _sdkDirectory;
    private static int _registered;

    public static string? SdkDirectory => _sdkDirectory;

    public static void Register()
    {
        if (Interlocked.Exchange(ref _registered, 1) != 0) return;

        _sdkDirectory = FindSdkDirectory();
        if (_sdkDirectory == null) return;

        AssemblyLoadContext.Default.Resolving += ResolveManagedAssembly;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += ResolveUnmanagedLibrary;
    }

    private static string? FindSdkDirectory()
    {
        var configuredPath = Environment.GetEnvironmentVariable(SdkPathEnvironmentVariable);
        var candidates = new[]
        {
            configuredPath,
            @"C:\Program Files (x86)\Genetec Security Center 5.12 SDK\net8.0-windows",
            @"C:\GenetecSdk\5.12\net8.0-windows",
            @"C:\Program Files (x86)\Genetec Security Center 5.13 SDK\net8.0-windows"
        };

        foreach (var candidate in candidates.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            try
            {
                var fullPath = Path.GetFullPath(candidate!);
                if (File.Exists(Path.Combine(fullPath, "Genetec.Sdk.dll"))) return fullPath;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Ignora un override opcional invalido y continua con las rutas conocidas.
            }
        }

        return null;
    }

    private static Assembly? ResolveManagedAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (_sdkDirectory == null || string.IsNullOrWhiteSpace(assemblyName.Name)) return null;

        var cultureDirectory = string.IsNullOrWhiteSpace(assemblyName.CultureName) ||
                               assemblyName.CultureName.Equals("neutral", StringComparison.OrdinalIgnoreCase)
            ? _sdkDirectory
            : Path.Combine(_sdkDirectory, assemblyName.CultureName);
        var candidate = Path.Combine(cultureDirectory, $"{Path.GetFileName(assemblyName.Name)}.dll");
        if (!File.Exists(candidate)) return null;

        try
        {
            return context.LoadFromAssemblyPath(candidate);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static nint ResolveUnmanagedLibrary(Assembly assembly, string libraryName)
    {
        if (_sdkDirectory == null) return nint.Zero;

        var safeLibraryName = Path.GetFileName(libraryName);
        var fileName = Path.HasExtension(safeLibraryName) ? safeLibraryName : $"{safeLibraryName}.dll";
        var searchDirectories = new[]
        {
            _sdkDirectory,
            Path.Combine(_sdkDirectory, "x64"),
            Path.Combine(_sdkDirectory, "amd64"),
            Path.Combine(_sdkDirectory, "runtimes", "win-x64", "native")
        };

        foreach (var directory in searchDirectories)
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle)) return handle;
        }

        return nint.Zero;
    }
}

