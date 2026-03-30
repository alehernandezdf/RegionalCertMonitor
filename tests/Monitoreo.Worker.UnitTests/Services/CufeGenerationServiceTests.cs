// BEGIN-TEST::BE-662::2026-03-17::AHL::Tests unitarios para CufeGenerationService: generación CUFE, obtención JWT, inyección en XML, fallo GetJWT
using System.Net;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Certification;
using Moq;
using Moq.Protected;

namespace Monitoreo.Worker.UnitTests.Services;

public class CufeGenerationServiceTests
{
    private readonly Mock<ILogger<CufeGenerationService>> _loggerMock = new();
    private const string SampleXml = "<Documento><Clave>TEST</Clave></Documento>";

    private static CountryConfig MakePaConfig() => new()
    {
        CountryCode = "PA", Enabled = true,
        AsmxEndpoint = "https://test", NucLoginEndpoint = "https://nuc.test",
        NucCertEndpoint = "https://nuc.test/cert", AsmxTemplatePath = "t.xml",
        NucTemplatePath = "t.xml", TaxId = "8-888-888", Requestor = "R",
        NucUsername = "u", NucAuthMode = "dynamic", RequiresCufe = true
    };

    private CufeGenerationService CreateService(HttpStatusCode statusCode, string responseBody)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://nuc.test") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("NucClient")).Returns(client);

        return new CufeGenerationService(factoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GenerateCufeAsync_HappyPath_ReturnsCufeAndJwt()
    {
        var svc = CreateService(HttpStatusCode.OK, """{"jwt":"eyJhbGciOiJIUzI1NiJ9.test"}""");
        var result = await svc.GenerateCufeAsync(SampleXml, MakePaConfig(), CancellationToken.None);

        result.Cufe.Should().NotBeNullOrWhiteSpace();
        result.Cufe.Should().HaveLength(64); // SHA256 hex
        result.Jwt.Should().StartWith("eyJ");
        result.UpdatedXml.Should().Contain("<CUFE>");
    }

    [Fact]
    public async Task GenerateCufeAsync_InjectsCufeInXml()
    {
        var svc = CreateService(HttpStatusCode.OK, """{"jwt":"eyJ.test"}""");
        var result = await svc.GenerateCufeAsync(SampleXml, MakePaConfig(), CancellationToken.None);

        var doc = XDocument.Parse(result.UpdatedXml);
        doc.Descendants("CUFE").Should().ContainSingle()
            .Which.Value.Should().Be(result.Cufe);
    }

    [Fact]
    public async Task GenerateCufeAsync_ExistingCufeNode_UpdatesValue()
    {
        var xmlWithCufe = "<Documento><CUFE>OLD</CUFE></Documento>";
        var svc = CreateService(HttpStatusCode.OK, """{"jwt":"eyJ.test"}""");
        var result = await svc.GenerateCufeAsync(xmlWithCufe, MakePaConfig(), CancellationToken.None);

        var doc = XDocument.Parse(result.UpdatedXml);
        doc.Descendants("CUFE").First().Value.Should().NotBe("OLD");
    }

    [Fact]
    public async Task GenerateCufeAsync_GetJwtFails_ThrowsHttpRequestException()
    {
        var svc = CreateService(HttpStatusCode.InternalServerError, "error");

        var act = () => svc.GenerateCufeAsync(SampleXml, MakePaConfig(), CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GenerateCufeAsync_GetJwtMissingField_ThrowsInvalidOperation()
    {
        var svc = CreateService(HttpStatusCode.OK, """{"token":"not-jwt-field"}""");

        var act = () => svc.GenerateCufeAsync(SampleXml, MakePaConfig(), CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }
}
// END-TEST::BE-662::2026-03-17::AHL::Tests unitarios para CufeGenerationService: generación CUFE, obtención JWT, inyección en XML, fallo GetJWT
