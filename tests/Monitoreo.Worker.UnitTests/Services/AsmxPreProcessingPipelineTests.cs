// BEGIN-TEST::BE-662::2026-03-17::AHL::Tests unitarios para AsmxPreProcessingPipeline: pipeline completo PA, parcial DO, sin pipeline GT/SV/CR
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Certification;

namespace Monitoreo.Worker.UnitTests.Services;

public class AsmxPreProcessingPipelineTests
{
    private readonly Mock<IPfxSigningService> _pfxMock = new();
    private readonly Mock<IQrGenerationService> _qrMock = new();
    private readonly Mock<ICufeGenerationService> _cufeMock = new();
    private readonly Mock<IAmazonSecretsManager> _secretsMock = new();
    private readonly Mock<ILogger<AsmxPreProcessingPipeline>> _loggerMock = new();

    private AsmxPreProcessingPipeline CreatePipeline() =>
        new(_pfxMock.Object, _qrMock.Object, _cufeMock.Object, _secretsMock.Object, _loggerMock.Object);

    private CountryConfig MakeConfig(bool pfx = false, bool qr = false, bool cufe = false) => new()
    {
        CountryCode = "PA", Enabled = true,
        AsmxEndpoint = "https://test", NucLoginEndpoint = "https://test",
        NucCertEndpoint = "https://test", AsmxTemplatePath = "t.xml",
        NucTemplatePath = "t.xml", TaxId = "123", Requestor = "R",
        NucUsername = "u", NucAuthMode = "dynamic",
        RequiresPfxSignature = pfx,
        PfxSecretArn = pfx ? "arn:pfx" : null,
        PfxPasswordSecretArn = pfx ? "arn:pfx-pwd" : null,
        RequiresQrGeneration = qr,
        QrCode = qr ? "QR" : null,
        RequiresCufe = cufe
    };

    [Fact]
    public async Task ProcessAsync_NoFlags_ReturnsXmlUnchanged()
    {
        var xml = "<doc>test</doc>";
        var sut = CreatePipeline();
        var result = await sut.ProcessAsync(xml, MakeConfig(), CancellationToken.None);
        result.Should().Be(xml);
    }

    [Fact]
    public async Task ProcessAsync_PfxOnly_CallsSigningService()
    {
        var xml = "<doc>test</doc>";
        var signed = "<doc>signed</doc>";
        _secretsMock.Setup(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetSecretValueResponse { SecretString = "base64pfx" });
        _pfxMock.Setup(p => p.SignXmlAsync(xml, "base64pfx", "base64pfx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(signed);

        var sut = CreatePipeline();
        var result = await sut.ProcessAsync(xml, MakeConfig(pfx: true), CancellationToken.None);

        result.Should().Be(signed);
        _qrMock.Verify(q => q.AddQrToXmlAsync(It.IsAny<string>(), It.IsAny<CountryConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_PfxFails_ThrowsWithoutContinuing()
    {
        _secretsMock.Setup(s => s.GetSecretValueAsync(It.IsAny<GetSecretValueRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("not found"));

        var sut = CreatePipeline();
        var act = () => sut.ProcessAsync("<doc/>", MakeConfig(pfx: true, qr: true, cufe: true), CancellationToken.None);

        await act.Should().ThrowAsync<ResourceNotFoundException>();
        _qrMock.Verify(q => q.AddQrToXmlAsync(It.IsAny<string>(), It.IsAny<CountryConfig>(), It.IsAny<CancellationToken>()), Times.Never);
        _cufeMock.Verify(c => c.GenerateCufeAsync(It.IsAny<string>(), It.IsAny<CountryConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
// END-TEST::BE-662::2026-03-17::AHL::Tests unitarios para AsmxPreProcessingPipeline: pipeline completo PA, parcial DO, sin pipeline GT/SV/CR
