// BEGIN-TEST::BE-661::2026-03-17::AHL::Tests unitarios para MonitoringOrchestrator: flujo completo con mock de gate y notificaciones
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Certification;
using Monitoreo.Worker.Services.Notification;
using Monitoreo.Worker.Services.Orchestration;
using Monitoreo.Worker.Services.Persistence;
using Monitoreo.Worker.Services.Observability;

namespace Monitoreo.Worker.UnitTests.Services;

public class MonitoringOrchestratorTests
{
    private readonly Mock<ICertificationService> _asmxCertMock = new();
    private readonly Mock<ICertificationService> _nucCertMock = new();
    private readonly Mock<IMonitoringRepository> _repoMock = new();
    private readonly Mock<INotificationService> _emailMock = new();
    private readonly Mock<INotificationGateService> _gateMock = new();
    private readonly Mock<IMetricsPublisher> _metricsMock = new();
    private readonly Mock<ILogger<MonitoringOrchestrator>> _loggerMock = new();

    private readonly CountryConfig _config = new()
    {
        CountryCode = "GT", Enabled = true, AlertThresholdMs = 30000,
        AsmxEndpoint = "https://test", NucLoginEndpoint = "https://test",
        NucCertEndpoint = "https://test", AsmxTemplatePath = "t.xml",
        NucTemplatePath = "t.xml", TaxId = "123", Requestor = "R",
        NucUsername = "u", NucAuthMode = "dynamic",
        EmailRecipients = ["test@example.com"], WhatsAppNumbers = ["+502123"]
    };

    private MonitoringOrchestrator CreateOrchestrator() => new(
        new[] { _asmxCertMock.Object, _nucCertMock.Object },
        _repoMock.Object,
        new[] { _emailMock.Object },
        _gateMock.Object,
        _metricsMock.Object,
        _loggerMock.Object);

    private MonitoringResult MakeResult(CertificationType type, bool status, long timeMs = 500) =>
        new(Guid.NewGuid(), "GT", type, "https://test", timeMs, status, status ? null : "Error", DateTimeOffset.UtcNow);

    [Fact]
    public async Task ExecuteCycleAsync_PersistsAllResults()
    {
        var asmxResult = MakeResult(CertificationType.ASMX, true);
        var nucResult = MakeResult(CertificationType.NUC, true);
        _asmxCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>())).ReturnsAsync(asmxResult);
        _nucCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>())).ReturnsAsync(nucResult);
        _asmxCertMock.SetupGet(c => c.Type).Returns(CertificationType.ASMX);
        _nucCertMock.SetupGet(c => c.Type).Returns(CertificationType.NUC);

        var sut = CreateOrchestrator();
        await sut.ExecuteCycleAsync(_config, CancellationToken.None);

        _repoMock.Verify(r => r.WriteResultAsync(It.IsAny<MonitoringResult>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteCycleAsync_FailedResult_TriggersNotificationGate()
    {
        var failResult = MakeResult(CertificationType.ASMX, false);
        _asmxCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>())).ReturnsAsync(failResult);
        _asmxCertMock.SetupGet(c => c.Type).Returns(CertificationType.ASMX);
        _nucCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(CertificationType.NUC, true));
        _nucCertMock.SetupGet(c => c.Type).Returns(CertificationType.NUC);

        _gateMock.Setup(g => g.EvaluateAsync("GT", "ASMX", It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationGateResult(true, null));

        var sut = CreateOrchestrator();
        await sut.ExecuteCycleAsync(_config, CancellationToken.None);

        _gateMock.Verify(g => g.EvaluateAsync("GT", "ASMX", It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteCycleAsync_GateSuppressed_DoesNotNotify()
    {
        var failResult = MakeResult(CertificationType.ASMX, false);
        _asmxCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>())).ReturnsAsync(failResult);
        _asmxCertMock.SetupGet(c => c.Type).Returns(CertificationType.ASMX);
        _nucCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(CertificationType.NUC, true));
        _nucCertMock.SetupGet(c => c.Type).Returns(CertificationType.NUC);

        _gateMock.Setup(g => g.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationGateResult(false, "Cooldown activo"));

        var sut = CreateOrchestrator();
        await sut.ExecuteCycleAsync(_config, CancellationToken.None);

        _emailMock.Verify(n => n.NotifyAsync(It.IsAny<NotificationPayload>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteCycleAsync_SuccessfulResults_NoNotifications()
    {
        _asmxCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(CertificationType.ASMX, true));
        _asmxCertMock.SetupGet(c => c.Type).Returns(CertificationType.ASMX);
        _nucCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(CertificationType.NUC, true));
        _nucCertMock.SetupGet(c => c.Type).Returns(CertificationType.NUC);

        var sut = CreateOrchestrator();
        await sut.ExecuteCycleAsync(_config, CancellationToken.None);

        _gateMock.Verify(g => g.EvaluateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteCycleAsync_CertificationThrows_ContinuesWithOther()
    {
        _asmxCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        _asmxCertMock.SetupGet(c => c.Type).Returns(CertificationType.ASMX);
        _nucCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(CertificationType.NUC, true));
        _nucCertMock.SetupGet(c => c.Type).Returns(CertificationType.NUC);

        var sut = CreateOrchestrator();
        await sut.ExecuteCycleAsync(_config, CancellationToken.None);

        _repoMock.Verify(r => r.WriteResultAsync(It.IsAny<MonitoringResult>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteCycleAsync_PublishesMetricsForEachResult()
    {
        _asmxCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(CertificationType.ASMX, true));
        _asmxCertMock.SetupGet(c => c.Type).Returns(CertificationType.ASMX);
        _nucCertMock.Setup(c => c.CertifyAsync(_config, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(CertificationType.NUC, true));
        _nucCertMock.SetupGet(c => c.Type).Returns(CertificationType.NUC);

        var sut = CreateOrchestrator();
        await sut.ExecuteCycleAsync(_config, CancellationToken.None);

        _metricsMock.Verify(m => m.PublishCertificationMetricAsync(It.IsAny<MonitoringResult>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
// END-TEST::BE-661::2026-03-17::AHL::Tests unitarios para MonitoringOrchestrator: flujo completo con mock de gate y notificaciones
