using SentinelBridge.Core.Services;

namespace SentinelBridge.Service.Tests.Services;

public sealed class GenetecApplicationCertificateLoaderTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "SentinelBridge.Tests",
        Guid.NewGuid().ToString("N"));

    public GenetecApplicationCertificateLoaderTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Load_ExtractsNamespacedApplicationIdAndFingerprint()
    {
        var path = WriteCertificate(
            "<Certificate xmlns=\"urn:genetec:test\"><ApplicationId> APP-ID-123 </ApplicationId></Certificate>");

        var result = GenetecApplicationCertificateLoader.Load(path);

        Assert.Equal("APP-ID-123", result.ApplicationId);
        Assert.Equal(12, result.Fingerprint.Length);
        Assert.Equal(Path.GetFullPath(path), result.SourcePath);
    }

    [Fact]
    public void Load_ReadsApplicationIdFromCertTextFile()
    {
        var path = WriteCertificate("APP-ID-FROM-DAP", ".cert");

        var result = GenetecApplicationCertificateLoader.Load(path);

        Assert.Equal("APP-ID-FROM-DAP", result.ApplicationId);
    }

    [Fact]
    public void Load_RejectsMissingFile()
    {
        var path = Path.Combine(_tempDirectory, "missing.xml");

        var exception = Assert.Throws<GenetecApplicationCertificateException>(
            () => GenetecApplicationCertificateLoader.Load(path));

        Assert.Contains("No se encontro", exception.Message);
    }

    [Fact]
    public void Load_RejectsCertificateWithoutApplicationId()
    {
        var path = WriteCertificate("<Certificate><Company>SentinelBridge</Company></Certificate>");

        var exception = Assert.Throws<GenetecApplicationCertificateException>(
            () => GenetecApplicationCertificateLoader.Load(path));

        Assert.Contains("no contiene un ApplicationId", exception.Message);
    }

    [Fact]
    public void Load_RejectsMultipleApplicationIds()
    {
        var path = WriteCertificate(
            "<Certificate><ApplicationId>ONE</ApplicationId><ApplicationId>TWO</ApplicationId></Certificate>");

        var exception = Assert.Throws<GenetecApplicationCertificateException>(
            () => GenetecApplicationCertificateLoader.Load(path));

        Assert.Contains("mas de un ApplicationId", exception.Message);
    }

    [Fact]
    public void Load_RejectsDtdContent()
    {
        var path = WriteCertificate(
            "<!DOCTYPE Certificate [<!ENTITY app \"APP-ID\">]><Certificate><ApplicationId>&app;</ApplicationId></Certificate>");

        Assert.Throws<GenetecApplicationCertificateException>(
            () => GenetecApplicationCertificateLoader.Load(path));
    }

    private string WriteCertificate(string content, string extension = ".xml")
    {
        var path = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}



