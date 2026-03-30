// BEGIN-FEAT::BE-666::2026-03-17::AHL::Servicio de notificación por WhatsApp via Graph API v17.0
using System.Text;
using System.Text.Json;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public class WhatsAppNotificationService : INotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WhatsAppNotificationService> _logger;
    private const string GraphApiUrl = "https://graph.facebook.com/v17.0";

    public WhatsAppNotificationService(
        IHttpClientFactory httpClientFactory,
        ILogger<WhatsAppNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyAsync(NotificationPayload payload, CancellationToken ct)
    {
        if (payload.Type != NotificationType.WhatsApp || payload.Recipients.Count == 0)
            return;

        var result = payload.Result;
        var client = _httpClientFactory.CreateClient("WhatsAppClient");

        foreach (var number in payload.Recipients)
        {
            var message = new
            {
                messaging_product = "whatsapp",
                to = number,
                type = "template",
                template = new
                {
                    name = "monitoring_response_mp",
                    language = new { code = "es" },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = new object[]
                            {
                                new { type = "text", text = result.Country },
                                new { type = "text", text = result.CertificationType.ToString() },
                                new { type = "text", text = result.ResultStatus ? "OK" : "FALLO" },
                                new { type = "text", text = $"{result.TransactionTimeMs}ms" },
                                new { type = "text", text = result.EventErrorMessage ?? "N/A" }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{GraphApiUrl}/me/messages", content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp enviado a {Number} para {Country}/{CertType}",
                    number, result.Country, result.CertificationType);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("WhatsApp falló para {Number}: {StatusCode} - {Error}",
                    number, response.StatusCode, errorBody[..Math.Min(200, errorBody.Length)]);
            }
        }
    }
}
// END-FEAT::BE-666::2026-03-17::AHL::Servicio de notificación por WhatsApp via Graph API v17.0
