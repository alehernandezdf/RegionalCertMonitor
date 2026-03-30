// BEGIN-FEAT::BE-665::2026-03-17::AHL::Servicio de notificación por email via Amazon SES
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public class EmailNotificationService : INotificationService
{
    private readonly IAmazonSimpleEmailServiceV2 _ses;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IAmazonSimpleEmailServiceV2 ses,
        ILogger<EmailNotificationService> logger)
    {
        _ses = ses;
        _logger = logger;
    }

    public async Task NotifyAsync(NotificationPayload payload, CancellationToken ct)
    {
        if (payload.Type != NotificationType.Email || payload.Recipients.Count == 0)
            return;

        var result = payload.Result;
        var subject = $"[Monitoreo {result.Country}] {result.CertificationType} - " +
                      (result.ResultStatus ? "OK" : "FALLO");

        var body = $"""
            País: {result.Country}
            Tipo: {result.CertificationType}
            Endpoint: {result.Endpoint}
            Tiempo: {result.TransactionTimeMs}ms
            Estado: {(result.ResultStatus ? "Exitoso" : "Fallido")}
            Error: {result.EventErrorMessage ?? "N/A"}
            Fecha: {result.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}
            """;

        var request = new SendEmailRequest
        {
            FromEmailAddress = "monitoreo@digifact.com",
            Destination = new Destination { ToAddresses = payload.Recipients.ToList() },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject },
                    Body = new Body { Text = new Content { Data = body } }
                }
            }
        };

        await _ses.SendEmailAsync(request, ct);
        _logger.LogInformation("Email enviado a {Count} destinatarios para {Country}/{CertType}",
            payload.Recipients.Count, result.Country, result.CertificationType);
    }
}
// END-FEAT::BE-665::2026-03-17::AHL::Servicio de notificación por email via Amazon SES
