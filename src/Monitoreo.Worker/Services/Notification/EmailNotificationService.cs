// BEGIN-FEAT::BE-665::2026-03-17::AHL::Servicio de notificación por email via SMTP (AWS SES SMTP)
using System.Net;
using System.Net.Mail;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public class EmailNotificationService : INotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IConfiguration configuration,
        ILogger<EmailNotificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task NotifyAsync(NotificationPayload payload, CancellationToken ct)
    {
        if (payload.Type != NotificationType.Email || payload.Recipients.Count == 0)
            return;

        var smtpServer = _configuration["Email:SmtpServer"];
        var smtpPort = _configuration.GetValue("Email:Port", 587);
        var smtpUser = _configuration["Email:Username"];
        var smtpPass = _configuration["Email:Password"];
        var fromAddress = _configuration["Email:FromAddress"] ?? "eface@digifact.com.gt";
        var fromName = _configuration["Email:FromName"] ?? "MONITOREO FEL DIGIFACT";

        if (string.IsNullOrWhiteSpace(smtpServer) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
        {
            _logger.LogWarning("Email NO enviado: configuracion SMTP incompleta (Email:SmtpServer/Username/Password)");
            return;
        }

        var result = payload.Result;
        var apiLabel = ApiLabel(result.CertificationType);
        var countryName = CountryName(result.Country);
        var tituloAccion = result.ResultStatus ? "DEMORA EN CERTIFICACION" : "ERROR EN CERTIFICACION";
        var subject = $"{tituloAccion} {apiLabel} {countryName}";
        var body = BuildHtmlBody(result, apiLabel, countryName, tituloAccion);

        using var msg = new MailMessage();
        msg.From = new MailAddress(fromAddress, fromName);
        foreach (var recipient in payload.Recipients)
            msg.To.Add(recipient);
        msg.Subject = subject;
        msg.Body = body;
        msg.IsBodyHtml = true;

        using var smtp = new SmtpClient(smtpServer, smtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(smtpUser, smtpPass)
        };

        await smtp.SendMailAsync(msg, ct);
        _logger.LogInformation("Email enviado a {Count} destinatarios para {Country}/{CertType}",
            payload.Recipients.Count, result.Country, result.CertificationType);
    }

    private static string BuildHtmlBody(MonitoringResult result, string apiLabel, string countryName, string tituloAccion)
    {
        // Respuesta completa del servicio (sin truncar). Si no hay, usa el mensaje de error.
        var respuesta = result.RawResponse;
        if (string.IsNullOrWhiteSpace(respuesta))
            respuesta = result.EventErrorMessage ?? "Sin respuesta del servicio";

        var respuestaHtml = System.Net.WebUtility.HtmlEncode(respuesta);

        return $"""
            <html><body style='font-family:Arial,sans-serif;'>
            <table width='900' bgcolor='#f2f2f2' style='border-collapse:collapse;'>
              <tr><td align='center' bgcolor='#D13438' style='color:white;padding:12px;font-size:16px;'><b>{tituloAccion} {apiLabel} {countryName}</b></td></tr>
              <tr><td style='padding:15px;'>
                <table width='100%' style='border-collapse:collapse;'>
                  <tr><td style='padding:6px;width:160px;'><b>API:</b></td><td style='padding:6px;'>{apiLabel}</td></tr>
                  <tr><td style='padding:6px;'><b>País:</b></td><td style='padding:6px;'>{countryName} ({result.Country})</td></tr>
                  <tr><td style='padding:6px;'><b>Estado:</b></td><td style='padding:6px;'>{(result.ResultStatus ? "Exitoso (demora)" : "Fallido")}</td></tr>
                  <tr><td style='padding:6px;'><b>Tiempo:</b></td><td style='padding:6px;'>{result.TransactionTimeMs} ms ({Math.Round(result.TransactionTimeMs / 1000.0, 2)} s)</td></tr>
                  <tr><td style='padding:6px;'><b>Endpoint:</b></td><td style='padding:6px;word-break:break-all;'>{result.Endpoint}</td></tr>
                  <tr><td style='padding:6px;'><b>Fecha:</b></td><td style='padding:6px;'>{result.CreatedAt:yyyy-MM-dd HH:mm:ss zzz}</td></tr>
                </table>
              </td></tr>
              <tr><td align='center' bgcolor='#D13438' style='color:white;padding:8px;font-size:14px;'><b>RESPUESTA COMPLETA DEL SERVICIO</b></td></tr>
              <tr><td style='padding:15px;'>
                <pre style='white-space:pre-wrap;word-break:break-word;color:#000000;font-size:12px;margin:0;'>{respuestaHtml}</pre>
              </td></tr>
            </table>
            </body></html>
            """;
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
}
// END-FEAT::BE-665::2026-03-17::AHL::Servicio de notificación por email via SMTP (AWS SES SMTP)
