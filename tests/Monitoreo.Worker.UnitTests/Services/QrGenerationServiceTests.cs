// BEGIN-TEST::BE-662::2026-03-17::AHL::Tests unitarios para QrGenerationService: inyección QR en XML, nodo existente, nodo nuevo, XML inválido
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Certification;
using Moq;

namespace Monitoreo.Worker.UnitTests.Services;

public class QrGenerationServiceTests
{
    private readonly QrGenerationService _svc;

    public QrGenerationServiceTests()
    {
        _svc = new QrGenerationService(new Mock<ILogger<QrGenerationService>>().Object);
    }

    private static CountryConfig MakePaConfig(string? qrCode = "QR_TEST_VALUE") => new()
    {
        CountryCode = "PA", Enabled = true,
        AsmxEndpoint = "https://test", NucLoginEndpoint = "https://test",
        NucCertEndpoint = "https://test", AsmxTemplatePath = "t.xml",
        NucTemplatePath = "t.xml", TaxId = "123", Requestor = "R",
        NucUsername = "u", NucAuthMode = "dynamic",
        RequiresQrGeneration = true, QrCode = qrCode
    };

    [Fact]
    public async Task AddQrToXmlAsync_ExistingNode_UpdatesValue()
    {
        var xml = "<Documento><ADDQR>OLD</ADDQR></Documento>";
        var result = await _svc.AddQrToXmlAsync(xml, MakePaConfig(), CancellationToken.None);

        var doc = XDocument.Parse(result);
        doc.Descendants("ADDQR").First().Value.Should().Be("QR_TEST_VALUE");
    }

    [Fact]
    public async Task AddQrToXmlAsync_NoExistingNode_AddsNewElement()
    {
        var xml = "<Documento><Clave>TEST</Clave></Documento>";
        var result = await _svc.AddQrToXmlAsync(xml, MakePaConfig(), CancellationToken.None);

        var doc = XDocument.Parse(result);
        doc.Descendants("ADDQR").Should().ContainSingle()
            .Which.Value.Should().Be("QR_TEST_VALUE");
    }

    [Fact]
    public async Task AddQrToXmlAsync_NullQrCode_SetsEmptyString()
    {
        var xml = "<Documento><ADDQR>OLD</ADDQR></Documento>";
        var result = await _svc.AddQrToXmlAsync(xml, MakePaConfig(null), CancellationToken.None);

        var doc = XDocument.Parse(result);
        doc.Descendants("ADDQR").First().Value.Should().BeEmpty();
    }

    [Fact]
    public async Task AddQrToXmlAsync_InvalidXml_ThrowsXmlException()
    {
        var act = () => _svc.AddQrToXmlAsync("not xml at all", MakePaConfig(), CancellationToken.None);
        await act.Should().ThrowAsync<System.Xml.XmlException>();
    }

    [Fact]
    public async Task AddQrToXmlAsync_CancellationRequested_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = () => _svc.AddQrToXmlAsync("<Doc/>", MakePaConfig(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
// END-TEST::BE-662::2026-03-17::AHL::Tests unitarios para QrGenerationService: inyección QR en XML, nodo existente, nodo nuevo, XML inválido
