// BEGIN-FEAT::BE-665::2026-03-25::AHL::Stub de notificaciones para desarrollo local (solo log a consola)
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public class ConsoleNotificationService : INotificationService
{
    private readonly ILogger<ConsoleNotificationService> _logger;

    public ConsoleNotificationService(ILogger<ConsoleNotificationService> logger) => _logger = logger;

    public Task NotifyAsync(NotificationPayload payload, CancellationToken ct)
    {
        _logger.LogWarning("[NOTIF-{Type}] {Country}/{CertType}: {Status} ({TimeMs}ms) → {Recipients}",
            payload.Type, payload.Result.Country, payload.Result.CertificationType,
            payload.Result.ResultStatus ? "OK" : "FAIL", payload.Result.TransactionTimeMs,
            string.Join(", ", payload.Recipients));
        return Task.CompletedTask;
    }
}
// END-FEAT::BE-665::2026-03-25::AHL::Stub de notificaciones para desarrollo local (solo log a consola)
