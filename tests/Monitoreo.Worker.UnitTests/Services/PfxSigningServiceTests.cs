// BEGIN-TEST::BE-662::2026-03-17::AHL::Tests unitarios para PfxSigningService: firma XML, PFX inválido, certificado expirado
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Monitoreo.Worker.Services.Certification;
using Moq;

namespace Monitoreo.Worker.UnitTests.Services;

public class PfxSigningServiceTests
{
    private readonly PfxSigningService _svc;
    private const string SampleXml = "<Documento><Clave>TEST</Clave></Documento>";

    public PfxSigningServiceTests()
    {
        _svc = new PfxSigningService(new Mock<ILogger<PfxSigningService>>().Object);
    }

    private static (string pfxBase64, string password) GenerateTestPfx(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(notBefore, notAfter);
        var password = "test123";
        var pfxBytes = cert.Export(X509ContentType.Pfx, password);
        return (Convert.ToBase64String(pfxBytes), password);
    }

    [Fact]
    public async Task SignXmlAsync_ValidPfx_ReturnsXmlWithSignature()
    {
        var (pfx, pwd) = GenerateTestPfx(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        var result = await _svc.SignXmlAsync(SampleXml, pfx, pwd, CancellationToken.None);

        result.Should().Contain("<Signature");
        result.Should().Contain("<Clave>TEST</Clave>");
    }

    [Fact]
    public async Task SignXmlAsync_ExpiredCert_ThrowsInvalidOperation()
    {
        var (pfx, pwd) = GenerateTestPfx(
            DateTimeOffset.UtcNow.AddYears(-2),
            DateTimeOffset.UtcNow.AddDays(-1));

        var act = () => _svc.SignXmlAsync(SampleXml, pfx, pwd, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expirado*");
    }

    [Fact]
    public async Task SignXmlAsync_InvalidBase64_ThrowsFormatException()
    {
        var act = () => _svc.SignXmlAsync(SampleXml, "not-valid-base64!!!", "pwd", CancellationToken.None);

        await act.Should().ThrowAsync<FormatException>();
    }

    [Fact]
    public async Task SignXmlAsync_CorruptPfxBytes_Throws()
    {
        var corruptBase64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5 });

        var act = () => _svc.SignXmlAsync(SampleXml, corruptBase64, "pwd", CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task SignXmlAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        var (pfx, pwd) = GenerateTestPfx(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _svc.SignXmlAsync(SampleXml, pfx, pwd, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
// END-TEST::BE-662::2026-03-17::AHL::Tests unitarios para PfxSigningService: firma XML, PFX inválido, certificado expirado
