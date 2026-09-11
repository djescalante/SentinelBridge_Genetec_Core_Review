# SentinelBridge — extracto de integración con Genetec

> **Idioma / Language:** Español | [English](README_EN.md)  
> **Documento técnico:** [DOCUMENTACION_TECNICA.md](DOCUMENTACION_TECNICA.md) ([English](DOCUMENTACION_TECNICA_EN.md))

Este paquete contiene únicamente el código necesario para revisar cómo SentinelBridge se autentica contra Genetec Security Center, se suscribe a los eventos del Platform SDK y normaliza los eventos recibidos. No es una copia completa de la solución.

## Alcance

- Plataforma: Windows x64 y .NET 8.
- SDK de referencia: Genetec Security Center Platform SDK 5.12 (`Genetec.Sdk.dll` 5.12.2181.44).
- Autenticación: usuario de Security Center más certificado/Application ID emitido para la aplicación mediante el programa DAP.
- Recepción: callback global `Engine.EventReceived`.
- Selección: filtrado local por identificador exacto de `EventType`.
- Normalización: tipo, origen, fecha/hora, grupo, entidad de origen y propiedades simples del objeto específico del evento.

## Qué revisar primero

1. `DOCUMENTACION_TECNICA.md`: arquitectura, secuencias y responsabilidades.
2. `Source/SentinelBridge.Core/Services/GenetecAuthenticationService.cs`: creación del `Engine`, certificado y `LogOnAsync`.
3. `Source/SentinelBridge.Core/Services/GenetecPlatformSdkClient.cs`: suscripción, filtrado y conversión de eventos.
4. `Source/SentinelBridge.Core/Models/GenetecEvent.cs`: contrato normalizado de salida.
5. `Tests/`: pruebas unitarias representativas.

## Exclusiones deliberadas

No se incluyen la interfaz gráfica, el servicio de Windows, el instalador, el tray app, el conector de salida, binarios propietarios del SDK, certificados, credenciales, logs ni datos del entorno. Tampoco se incluye una prueba que requiera un servidor real.

El archivo compartido `ServiceConfiguration.cs` se conserva para que las firmas sean fieles al código de producción; las secciones ajenas a `Genetec` no forman parte de esta revisión.

## Sanitización

El código no fue ofuscado: los nombres de clases, métodos y flujo deben permanecer legibles para una revisión técnica. Se sustituyeron valores de prueba que parecían credenciales por marcadores y se excluyeron todos los artefactos operativos. El ejemplo de configuración contiene únicamente placeholders.

## Compilación del extracto

Requisitos:

- Windows x64.
- .NET SDK 8.
- Genetec Platform SDK compatible instalado o una ruta local autorizada que contenga `Genetec.Sdk.dll`.

Ejemplo:

```powershell
dotnet build .\Source\SentinelBridge.Core\SentinelBridge.Core.csproj `
  -c Release `
  -p:GenetecSdkPath="C:\ruta\autorizada\al\sdk\net8.0-windows"
```

Los binarios del SDK no se redistribuyen en este paquete. Las pruebas se entregan como evidencia revisable; para ejecutarlas se requiere el proyecto de pruebas original, sus paquetes NuGet y el SDK correspondiente.

## Configuración de referencia

Consulte `Configuration/appsettings.genetec.example.json`. Los valores entre `<...>` deben reemplazarse solo en un entorno autorizado; no deben enviarse credenciales reales junto con este paquete.

