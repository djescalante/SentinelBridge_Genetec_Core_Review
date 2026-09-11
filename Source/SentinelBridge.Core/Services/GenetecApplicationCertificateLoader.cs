using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace SentinelBridge.Core.Services;

public sealed record GenetecApplicationCertificate(
    string ApplicationId,
    string Fingerprint,
    string SourcePath);

public static class GenetecApplicationCertificateLoader
{
    public static GenetecApplicationCertificate Load(string certificatePath)
    {
        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            throw new GenetecApplicationCertificateException(
                "Configure el archivo del certificado de aplicacion SDK de Genetec.");
        }

        string fullPath;
        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(certificatePath.Trim());
            fullPath = Path.GetFullPath(expandedPath, AppContext.BaseDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new GenetecApplicationCertificateException(
                "La ruta del certificado SDK de Genetec no es valida.", ex);
        }

        if (!File.Exists(fullPath))
        {
            throw new GenetecApplicationCertificateException(
                $"No se encontro el certificado de aplicacion SDK de Genetec en '{fullPath}'.");
        }

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            var content = File.ReadAllText(fullPath).Trim();
            if (content.Length == 0)
            {
                throw new GenetecApplicationCertificateException(
                    "El certificado SDK de Genetec esta vacio.");
            }

            var applicationIds = content.StartsWith('<')
                ? ExtractApplicationIdsFromXml(content, settings)
                : ExtractApplicationIdFromText(content);

            if (applicationIds.Length != 1)
            {
                throw new GenetecApplicationCertificateException(
                    applicationIds.Length == 0
                        ? "El certificado SDK de Genetec no contiene un ApplicationId."
                        : "El certificado SDK de Genetec contiene mas de un ApplicationId.");
            }

            var applicationId = applicationIds[0];
            var fingerprintBytes = SHA256.HashData(Encoding.UTF8.GetBytes(applicationId));
            var fingerprint = Convert.ToHexString(fingerprintBytes)[..12];
            return new GenetecApplicationCertificate(applicationId, fingerprint, fullPath);
        }
        catch (GenetecApplicationCertificateException)
        {
            throw;
        }
        catch (Exception ex) when (ex is XmlException or IOException or UnauthorizedAccessException)
        {
            throw new GenetecApplicationCertificateException(
                "No se pudo leer el certificado SDK de Genetec. Verifique el formato y los permisos del archivo.",
                ex);
        }
    }

    private static string[] ExtractApplicationIdsFromXml(
        string content,
        XmlReaderSettings settings)
    {
        using var stringReader = new StringReader(content);
        using var reader = XmlReader.Create(stringReader, settings);
        var document = XDocument.Load(reader, LoadOptions.None);
        return (document.Root?.DescendantsAndSelf() ?? Enumerable.Empty<XElement>())
            .Where(element => string.Equals(
                element.Name.LocalName,
                "ApplicationId",
                StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractApplicationIdFromText(string content)
    {
        if (content.Any(char.IsControl))
        {
            throw new GenetecApplicationCertificateException(
                "El certificado SDK de Genetec no contiene un identificador de aplicacion valido.");
        }

        return [content];
    }
}

public sealed class GenetecApplicationCertificateException : Exception
{
    public GenetecApplicationCertificateException(string message) : base(message) { }
    public GenetecApplicationCertificateException(string message, Exception innerException)
        : base(message, innerException) { }
}

