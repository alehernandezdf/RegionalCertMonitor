// BEGIN-FEAT::BE-667::2026-03-25::AHL::Gate de notificaciones local para desarrollo sin SSM
using System.Collections.Concurrent;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public class LocalNotificationGateService : INotificationGateService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastNotificationTimes = new();
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalNotificationGateService> _logger;

    public LocalNotificationGateService(IConfiguration configuration, ILogger<LocalNotificationGateService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<NotificationGateResult> EvaluateAsync(
        string countryCode, string certType, NotificationChannel channel, CancellationToken ct)
    {
        // BEGIN-FIX::BE-672::2026-07-01::AHL::Kill switch global: apagado por defecto para que corridas locales NUNCA manden alertas. El server lo enciende con Notifications__Enabled=true (archivo .env)
        if (!_configuration.GetValue("Notifications:Enabled", false))
        {
            _logger.LogDebug("[GATE] Suprimido: Notifications:Enabled=false (entorno local/no productivo)");
            return Task.FromResult(new NotificationGateResult(false, "Notificaciones deshabilitadas (Notifications:Enabled=false)"));
        }
        // END-FIX::BE-672::2026-07-01::AHL::Kill switch global

        var key = $"{countryCode}_{certType}_{channel}";
        _lastNotificationTimes[key] = DateTimeOffset.UtcNow;

        _logger.LogDebug("[GATE] Permitido: {Key}", key);
        return Task.FromResult(new NotificationGateResult(true, null));
    }
}
// END-FEAT::BE-667::2026-03-25::AHL::Gate de notificaciones local para desarrollo sin SSM
