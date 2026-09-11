# SentinelBridge — Genetec Integration Code Excerpt

> **Language / Idioma:** English | [Español](README.md)  
> **Technical Document:** [DOCUMENTACION_TECNICA_EN.md](DOCUMENTACION_TECNICA_EN.md) ([Español](DOCUMENTACION_TECNICA.md))

This package contains solely the code necessary to review how SentinelBridge authenticates against Genetec Security Center, subscribes to Platform SDK events, and normalizes received events. It is not a complete copy of the solution.

## Scope

- **Platform:** Windows x64 and .NET 8.
- **Reference SDK:** Genetec Security Center Platform SDK 5.12 (`Genetec.Sdk.dll` 5.12.2181.44).
- **Authentication:** Security Center user account plus application certificate/Application ID issued via the DAP program.
- **Reception:** Global callback `Engine.EventReceived`.
- **Selection:** Local filtering by exact `EventType` identifier.
- **Normalization:** Type, source, timestamp, group, source entity, and safe simple properties from the event's specific object.

## What to Review First

1. `DOCUMENTACION_TECNICA_EN.md`: Architecture, sequences, and component responsibilities.
2. `Source/SentinelBridge.Core/Services/GenetecAuthenticationService.cs`: `Engine` instantiation, certificate loading, and `LogOnAsync`.
3. `Source/SentinelBridge.Core/Services/GenetecPlatformSdkClient.cs`: Subscription lifecycle, filtering, and event conversion.
4. `Source/SentinelBridge.Core/Models/GenetecEvent.cs`: Normalized output contract.
5. `Tests/`: Representative unit test suite.

## Deliberate Exclusions

The graphical user interface (GUI), Windows service wrapper, installer, system tray application, outbound destination connector, proprietary SDK binaries, production certificates, credentials, logs, and customer environment data are excluded. Tests requiring a live server are also excluded.

The shared `ServiceConfiguration.cs` file is preserved to keep method and constructor signatures identical to production code; configuration sections unrelated to `Genetec` are not part of this review.

## Sanitization

The source code has not been obfuscated: class names, methods, and execution flows remain fully legible for technical evaluation. Test values that resembled credentials have been replaced with placeholders, and all operational runtime artifacts have been removed. Configuration examples contain placeholders only.

## Excerpt Compilation

Requirements:

- Windows x64.
- .NET SDK 8.
- Compatible Genetec Platform SDK installed, or an authorized local directory containing `Genetec.Sdk.dll`.

Example build command:

```powershell
dotnet build .\Source\SentinelBridge.Core\SentinelBridge.Core.csproj `
  -c Release `
  -p:GenetecSdkPath="C:\authorized\path\to\sdk\net8.0-windows"
```

SDK binaries are not redistributed within this package. The unit tests are provided as reviewable evidence; executing them requires the original test runner project, respective NuGet packages, and the referenced SDK.

## Reference Configuration

Refer to `Configuration/appsettings.genetec.example.json`. Values enclosed in `<...>` must be substituted only within an authorized test environment; no live credentials should ever be transmitted alongside this package.
