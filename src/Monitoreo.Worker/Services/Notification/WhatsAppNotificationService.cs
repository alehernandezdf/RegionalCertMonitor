// BEGIN-FEAT::BE-666::2026-03-17::AHL::Servicio de notificación por WhatsApp via Graph API (Meta WhatsApp Business)
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public class WhatsAppNotificationService : INotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppNotificationService> _logger;

    public WhatsAppNotificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WhatsAppNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyAsync(NotificationPayload payload, CancellationToken ct)
    {
        if (payload.Type != NotificationType.WhatsApp || payload.Recipients.Count == 0)
            return;

        var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
        var token = _configuration["WhatsApp:Token"];
        var templateName = _configuration["WhatsApp:TemplateName"] ?? "monitoring_response_mp";
        var apiVersion = _configuration["WhatsApp:ApiVersion"] ?? "v17.0";

        if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("WhatsApp NO enviado: configuracion incompleta (WhatsApp:PhoneNumberId/Token)");
            return;
        }

        var result = payload.Result;
        var client = _httpClientFactory.CreateClient("WhatsAppClient");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var url = $"https://graph.facebook.com/{apiVersion}/{phoneNumberId}/messages";

        foreach (var number in payload.Recipients)
        {
            var message = new
            {
                messaging_product = "whatsapp",
                to = number,
                type = "template",
                template = new
                {
                    name = templateName,
                    language = new { code = "es" },
                    components = new[]
                    {
                        new
                        {
                            type = "body",
                            parameters = new object[]
                            {
                                new { type = "text", text = $"{CountryFlag(result.Country)} {CountryName(result.Country)}" },
                                new { type = "text", text = ApiLabel(result.CertificationType) },
                                new { type = "text", text = result.ResultStatus ? "OK" : "FALLO" },
                                new { type = "text", text = $"{result.TransactionTimeMs}ms" },
                                new { type = "text", text = Sanitize(result.EventErrorMessage) }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(message);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("WhatsApp enviado a {Number} para {Country}/{CertType}",
                    number, result.Country, result.CertificationType);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("WhatsApp falló para {Number}: {StatusCode} - {Error}",
                    number, response.StatusCode, errorBody[..Math.Min(300, errorBody.Length)]);
            }
        }
    }

    // Los parametros de plantilla de WhatsApp no aceptan saltos de linea ni tabs, y tienen limite de longitud.
    // Se recorta a 200 chars: el WhatsApp es el aviso rapido, el detalle completo va por correo.
    private static string Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "N/A";
        var clean = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        while (clean.Contains("  ")) clean = clean.Replace("  ", " ");
        return clean.Length > 200 ? clean[..200] : clean;
    }

    private static string ApiLabel(CertificationType type) => type switch
    {
        CertificationType.ASMX => "ASMX",
        CertificationType.NUC => "API NUC",
        CertificationType.API => "API V3",
        _ => type.ToString()
    };

    private static string CountryName(string code) => code switch
    {
        "GT" => "GUATEMALA",
        "GT2" => "GUATEMALA",
        "SV" => "EL SALVADOR",
        "CR" => "COSTA RICA",
        "DO" => "REPUBLICA DOMINICANA",
        "PA" => "PANAMA",
        _ => code
    };

    private static string CountryFlag(string code) => code switch
    {
        "GT" => "🇬🇹",
        "GT2" => "🇬🇹",
        "SV" => "🇸🇻",
        "CR" => "🇨🇷",
        "DO" => "🇩🇴",
        "PA" => "🇵🇦",
        _ => "🌐"
    };
}
// END-FEAT::BE-666::2026-03-17::AHL::Servicio de notificación por WhatsApp via Graph API (Meta WhatsApp Business)
