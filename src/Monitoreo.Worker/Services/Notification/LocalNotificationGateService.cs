// BEGIN-FEAT::BE-667::2026-03-25::AHL::Gate de notificaciones local para desarrollo sin SSM
using System.Collections.Concurrent;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public class LocalNotificationGateService : INotificationGateService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastNotificationTimes = new();
    private readonly ILogger<LocalNotificationGateService> _logger;

    public LocalNotificationGateService(ILogger<LocalNotificationGateService> logger) => _logger = logger;

    public Task<NotificationGateResult> EvaluateAsync(
        string countryCode, string certType, NotificationChannel channel, CancellationToken ct)
    {
        // En desarrollo: siempre permitir (sin SSM, sin cooldown estricto)
        var key = $"{countryCode}_{certType}_{channel}";
        _lastNotificationTimes[key] = DateTimeOffset.UtcNow;

        _logger.LogDebug("[GATE] Permitido: {Key}", key);
        return Task.FromResult(new NotificationGateResult(true, null));
    }
}
// END-FEAT::BE-667::2026-03-25::AHL::Gate de notificaciones local para desarrollo sin SSM
