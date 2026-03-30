// BEGIN-TEST::BE-665::2026-03-17::AHL::Tests unitarios para EmailNotificationService: mock SES, destinatarios, contenido, tipos
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Notification;
using Moq;

namespace Monitoreo.Worker.UnitTests.Services;

public class EmailNotificationServiceTests
{
    private readonly Mock<IAmazonSimpleEmailServiceV2> _sesMock = new();
    private readonly Mock<ILogger<EmailNotificationService>> _loggerMock = new();
    private readonly EmailNotificationService _svc;

    public EmailNotificationServiceTests()
    {
        _sesMock.Setup(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendEmailResponse { MessageId = "msg-001" });
        _svc = new EmailNotificationService(_sesMock.Object, _loggerMock.Object);
    }

    private static MonitoringResult MakeResult(bool success = false) => new(
        Guid.NewGuid(), "GT", CertificationType.ASMX,
        "https://asmx.test/gt", 1500, success,
        success ? null : "Timeout", DateTimeOffset.UtcNow);

    [Fact]
    public async Task NotifyAsync_SendsEmail_ToAllRecipients()
    {
        var payload = new NotificationPayload(MakeResult(), NotificationType.Email,
            ["test1@example.com", "test2@example.com"]);

        await _svc.NotifyAsync(payload, CancellationToken.None);

        _sesMock.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r => r.Destination.ToAddresses.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_FailResult_SubjectContainsFallo()
    {
        var payload = new NotificationPayload(MakeResult(false), NotificationType.Email, ["a@b.com"]);
        await _svc.NotifyAsync(payload, CancellationToken.None);

        _sesMock.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r => r.Content.Simple.Subject.Data.Contains("FALLO")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_SuccessResult_SubjectContainsOK()
    {
        var payload = new NotificationPayload(MakeResult(true), NotificationType.Email, ["a@b.com"]);
        await _svc.NotifyAsync(payload, CancellationToken.None);

        _sesMock.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r => r.Content.Simple.Subject.Data.Contains("OK")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_WhatsAppType_DoesNotSendEmail()
    {
        var payload = new NotificationPayload(MakeResult(), NotificationType.WhatsApp, ["a@b.com"]);
        await _svc.NotifyAsync(payload, CancellationToken.None);

        _sesMock.Verify(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_EmptyRecipients_DoesNotSendEmail()
    {
        var payload = new NotificationPayload(MakeResult(), NotificationType.Email, []);
        await _svc.NotifyAsync(payload, CancellationToken.None);

        _sesMock.Verify(s => s.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyAsync_BodyContainsCountryAndEndpoint()
    {
        var payload = new NotificationPayload(MakeResult(), NotificationType.Email, ["a@b.com"]);
        await _svc.NotifyAsync(payload, CancellationToken.None);

        _sesMock.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r =>
                r.Content.Simple.Body.Text.Data.Contains("GT") &&
                r.Content.Simple.Body.Text.Data.Contains("https://asmx.test/gt")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_FromAddress_IsMonitoreo()
    {
        var payload = new NotificationPayload(MakeResult(), NotificationType.Email, ["a@b.com"]);
        await _svc.NotifyAsync(payload, CancellationToken.None);

        _sesMock.Verify(s => s.SendEmailAsync(
            It.Is<SendEmailRequest>(r => r.FromEmailAddress == "monitoreo@digifact.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
// END-TEST::BE-665::2026-03-17::AHL::Tests unitarios para EmailNotificationService: mock SES, destinatarios, contenido, tipos
