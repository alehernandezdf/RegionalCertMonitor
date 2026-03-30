// FEAT::BE-667::2026-03-25::AHL::No-op notification gate para desarrollo local sin AWS SSM
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public class NoOpNotificationGateService : INotificationGateService
{
    private readonly ILogger<NoOpNotificationGateService> _logger;

    public NoOpNotificationGateService(ILogger<NoOpNotificationGateService> logger) => _logger = logger;

    public Task<NotificationGateResult> EvaluateAsync(
        string countryCode, string certType, NotificationChannel channel, CancellationToken ct)
    {
        _logger.LogDebug("[NoOp] Notificación suprimida (modo local): {Country}/{CertType}/{Channel}",
            countryCode, certType, channel);
        return Task.FromResult(new NotificationGateResult(false, "Modo desarrollo local - notificaciones deshabilitadas"));
    }
}
