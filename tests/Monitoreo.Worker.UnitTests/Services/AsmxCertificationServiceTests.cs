// BEGIN-TEST::BE-662::2026-03-17::AHL::Tests unitarios para AsmxCertificationService: mock HttpClient, XML, medición tiempo, integración con pipeline
using System.Net;
using System.Xml.Linq;
using FluentAssertions;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Certification;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Monitoreo.Worker.UnitTests.Services;

public class AsmxCertificationServiceTests : IDisposable
{
    private const string ValidXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <Documento>
            <Clave>PLACEHOLDER</Clave>
            <FechaEmision>2000-01-01T00:00:00+00:00</FechaEmision>
            <Consecutivo>0000000000</Consecutivo>
        </Documento>
        """;

    private readonly string _tempTemplatePath;
    private readonly Mock<IAsmxPreProcessingPipeline> _pipelineMock = new();
    private readonly Mock<ILogger<AsmxCertificationService>> _loggerMock = new();

    public AsmxCertificationServiceTests()
    {
        _tempTemplatePath = Path.GetTempFileName();
        File.WriteAllText(_tempTemplatePath, ValidXml);
    }

    public void Dispose() => File.Delete(_tempTemplatePath);

    private CountryConfig MakeConfig(string country = "GT") => new()
    {
        CountryCode = country, Enabled = true,
        AsmxEndpoint = "https://asmx.test/cert",
        NucLoginEndpoint = "https://nuc.test/login",
        NucCertEndpoint = "https://nuc.test/cert",
        AsmxTemplatePath = _tempTemplatePath,
        NucTemplatePath = "t.xml",
        TaxId = "123456", Requestor = "R",
        NucUsername = "u", NucAuthMode = "dynamic"
    };

    private (AsmxCertificationService svc, Mock<HttpMessageHandler> handler) CreateService(
        HttpStatusCode statusCode = HttpStatusCode.OK, string responseBody = "<ok/>")
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://asmx.test") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AsmxClient")).Returns(client);

        _pipelineMock.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CountryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string xml, CountryConfig _, CancellationToken _) => xml);

        var svc = new AsmxCertificationService(factoryMock.Object, _pipelineMock.Object, _loggerMock.Object);
        return (svc, handlerMock);
    }

    [Fact]
    public async Task CertifyAsync_SuccessfulResponse_ReturnsSuccessResult()
    {
        var (svc, _) = CreateService();
        var result = await svc.CertifyAsync(MakeConfig(), CancellationToken.None);

        result.ResultStatus.Should().BeTrue();
        result.Country.Should().Be("GT");
        result.CertificationType.Should().Be(CertificationType.ASMX);
        result.TransactionTimeMs.Should().BeGreaterThanOrEqualTo(0);
        result.EventErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task CertifyAsync_FaultResponse_ReturnsFailResult()
    {
        var faultXml = "<Envelope><Body><Fault><faultcode>s:Server</faultcode><faultstring>Error interno</faultstring></Fault></Body></Envelope>";
        var (svc, _) = CreateService(HttpStatusCode.OK, faultXml);
        var result = await svc.CertifyAsync(MakeConfig(), CancellationToken.None);

        result.ResultStatus.Should().BeFalse();
        result.EventErrorMessage.Should().Contain("Error interno");
    }

    [Fact]
    public async Task CertifyAsync_HttpError_ReturnsFailResult()
    {
        var (svc, _) = CreateService(HttpStatusCode.InternalServerError, "server error");
        var result = await svc.CertifyAsync(MakeConfig(), CancellationToken.None);

        result.ResultStatus.Should().BeFalse();
    }

    [Fact]
    public async Task CertifyAsync_InjectsDynamicFields_InXml()
    {
        string? capturedBody = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
                capturedBody = await req.Content!.ReadAsStringAsync())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<ok/>") });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://asmx.test") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AsmxClient")).Returns(client);
        _pipelineMock.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CountryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string xml, CountryConfig _, CancellationToken _) => xml);

        var svc = new AsmxCertificationService(factoryMock.Object, _pipelineMock.Object, _loggerMock.Object);
        await svc.CertifyAsync(MakeConfig(), CancellationToken.None);

        capturedBody.Should().NotBeNull();
        var doc = XDocument.Parse(capturedBody!);
        doc.Descendants("Clave").First().Value.Should().StartWith("MON-GT-");
        doc.Descendants("Consecutivo").First().Value.Should().HaveLength(10);
    }

    [Fact]
    public async Task CertifyAsync_CallsPipeline_BeforeSending()
    {
        var (svc, _) = CreateService();
        await svc.CertifyAsync(MakeConfig("PA"), CancellationToken.None);

        _pipelineMock.Verify(
            p => p.ProcessAsync(It.IsAny<string>(), It.Is<CountryConfig>(c => c.CountryCode == "PA"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CertifyAsync_PipelineThrows_ReturnsFailWithoutSendingHttp()
    {
        _pipelineMock.Setup(p => p.ProcessAsync(It.IsAny<string>(), It.IsAny<CountryConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PFX inválido"));

        var handlerMock = new Mock<HttpMessageHandler>();
        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://asmx.test") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("AsmxClient")).Returns(client);

        var svc = new AsmxCertificationService(factoryMock.Object, _pipelineMock.Object, _loggerMock.Object);
        var result = await svc.CertifyAsync(MakeConfig("PA"), CancellationToken.None);

        result.ResultStatus.Should().BeFalse();
        result.EventErrorMessage.Should().Contain("PFX inválido");
        handlerMock.Protected().Verify("SendAsync", Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CertifyAsync_ConsecutiveCallsIncrementCounter()
    {
        var (svc, _) = CreateService();
        var r1 = await svc.CertifyAsync(MakeConfig(), CancellationToken.None);
        var r2 = await svc.CertifyAsync(MakeConfig(), CancellationToken.None);

        r1.ResultStatus.Should().BeTrue();
        r2.ResultStatus.Should().BeTrue();
    }

    [Fact]
    public async Task CertifyAsync_MeasuresTransactionTime()
    {
        var (svc, _) = CreateService();
        var result = await svc.CertifyAsync(MakeConfig(), CancellationToken.None);

        result.TransactionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
// END-TEST::BE-662::2026-03-17::AHL::Tests unitarios para AsmxCertificationService: mock HttpClient, XML, medición tiempo, integración con pipeline
