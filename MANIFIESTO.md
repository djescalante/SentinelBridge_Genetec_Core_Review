# Manifiesto del paquete

## Código de producción

- `Source/build/GenetecSdk.props`
- `Source/SentinelBridge.Core/SentinelBridge.Core.csproj`
- `Source/SentinelBridge.Core/Configuration/ServiceConfiguration.cs`
- `Source/SentinelBridge.Core/Interfaces/IConfigurationManager.cs`
- `Source/SentinelBridge.Core/Interfaces/IGenetecEventSource.cs`
- `Source/SentinelBridge.Core/Models/GenetecEvent.cs`
- `Source/SentinelBridge.Core/Models/GenetecEventCatalog.cs`
- `Source/SentinelBridge.Core/Models/GenetecEventSpanishLocalizer.cs`
- `Source/SentinelBridge.Core/Models/GenetecSdkEventTypeIds.g.cs`
- `Source/SentinelBridge.Core/Models/ServiceOperationalStatus.cs`
- `Source/SentinelBridge.Core/Services/GenetecApplicationCertificateLoader.cs`
- `Source/SentinelBridge.Core/Services/GenetecAuthenticationService.cs`
- `Source/SentinelBridge.Core/Services/GenetecEventSource.cs`
- `Source/SentinelBridge.Core/Services/GenetecPlatformSdkClient.cs`
- `Source/SentinelBridge.Core/Services/GenetecSdkAssemblyResolver.cs`
- `Source/SentinelBridge.Core/Services/GenetecSdkDiagnostics.cs`
- `Source/SentinelBridge.Core/Services/ServiceOperationalStatusStore.cs`

## Pruebas de referencia

- `Tests/Connectors/Genetec/PlatformSdkClientTests.cs`
- `Tests/Connectors/Genetec/TestUtilities.cs`
- `Tests/Services/GenetecApplicationCertificateLoaderTests.cs`
- `Tests/Services/GenetecAuthenticationServiceTests.cs`
- `Tests/Services/GenetecEventCatalogTests.cs`

## Material auxiliar

- `Configuration/appsettings.genetec.example.json`
- `README.md` (Español)
- `README_EN.md` (English)
- `DOCUMENTACION_TECNICA.md` (Español)
- `DOCUMENTACION_TECNICA_EN.md` (English)
- `Docs/DOCUMENTACION_TECNICA.md` (Español)
- `Docs/DOCUMENTACION_TECNICA_EN.md` (English)
- `Docs/Documento_Tecnico_SentinelBridge.pdf`
- `MANIFIESTO.md`

## No incluido

- binarios o documentación propietaria de Genetec;
- certificado/Application ID real;
- credenciales, hosts, IP o rutas del ambiente del cliente;
- configuración operativa, logs o archivos de estado;
- UI, tray app, servicio de Windows, instalador y conector de destino;
- pruebas de integración que requieran un servidor real.

