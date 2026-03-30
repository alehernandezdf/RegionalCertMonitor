// BEGIN-TEST::BE-666::2026-03-17::AHL::Tests unitarios para WhatsAppNotificationService: mock HttpClient, payload JSON, template WhatsApp
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Notification;
using Moq;
using Moq.Protected;

namespace Monitoreo.Worker.UnitTests.Services;

public class WhatsAppNotificationServiceTests
{
    private readonly Mock<ILogger<WhatsAppNotificationService>> _loggerMock = new();

    private static MonitoringResult MakeResult(bool success = false) => new(
        Guid.NewGuid(), "SV", CertificationType.NUC,
        "https://nuc.test/sv", 2300, success,
        success ? null : "Connection refused", DateTimeOffset.UtcNow);

    private (WhatsAppNotificationService svc, List<string> capturedBodies) CreateService(
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var captured = new List<string>();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                if (req.Content != null)
                    captured.Add(await req.Content.ReadAsStringAsync());
            })
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\"}]}")
            });

        var client = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://graph.facebook.com") };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient("WhatsAppClient")).Returns(client);

        return (new WhatsAppNotificationService(factoryMock.Object, _loggerMock.Object), captured);
    }

    [Fact]
    public async Task NotifyAsync_SendsToEachRecipient()
    {
        var (svc, captured) = CreateService();
        var payload = new NotificationPayload(MakeResult(), NotificationType.WhatsApp,
            ["+50212345678", "+50287654321"]);

        await svc.NotifyAsync(payload, CancellationToken.None);

        captured.Should().HaveCount(2);
    }

    [Fact]
    public async Task NotifyAsync_PayloadContainsTemplate()
    {
        var (svc, captured) = CreateService();
        var payload = new NotificationPayload(MakeResult(), NotificationType.WhatsApp, ["+50212345678"]);

        await svc.NotifyAsync(payload, CancellationToken.None);

        captured.Should().ContainSingle();
        using var doc = JsonDocument.Parse(captured[0]);
        doc.RootElement.GetProperty("template").GetProperty("name").GetString()
            .Should().Be("monitoring_response_mp");
        doc.RootElement.GetProperty("messaging_product").GetString()
            .Should().Be("whatsapp");
    }

    [Fact]
    public async Task NotifyAsync_PayloadContainsCountryAndStatus()
    {
        var (svc, captured) = CreateService();
        var payload = new NotificationPayload(MakeResult(false), NotificationType.WhatsApp, ["+50212345678"]);

        await svc.NotifyAsync(payload, CancellationToken.None);

        var body = captured[0];
        body.Should().Contain("SV");
        body.Should().Contain("FALLO");
    }

    [Fact]
    public async Task NotifyAsync_EmailType_DoesNotSend()
    {
        var (svc, captured) = CreateService();
        var payload = new NotificationPayload(MakeResult(), NotificationType.Email, ["+50212345678"]);

        await svc.NotifyAsync(payload, CancellationToken.None);

        captured.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_EmptyRecipients_DoesNotSend()
    {
        var (svc, captured) = CreateService();
        var payload = new NotificationPayload(MakeResult(), NotificationType.WhatsApp, []);

        await svc.NotifyAsync(payload, CancellationToken.None);

        captured.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_HttpError_DoesNotThrow()
    {
        var (svc, _) = CreateService(HttpStatusCode.Unauthorized);
        var payload = new NotificationPayload(MakeResult(), NotificationType.WhatsApp, ["+50212345678"]);

        var act = () => svc.NotifyAsync(payload, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
// END-TEST::BE-666::2026-03-17::AHL::Tests unitarios para WhatsAppNotificationService: mock HttpClient, payload JSON, template WhatsApp
