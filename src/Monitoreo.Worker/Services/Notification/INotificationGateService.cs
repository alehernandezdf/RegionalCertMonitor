// BEGIN-FEAT::BE-667::2026-03-17::AHL::Interfaz del gate de control manual de notificaciones
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public interface INotificationGateService
{
    Task<NotificationGateResult> EvaluateAsync(
        string countryCode, string certType, NotificationChannel channel, CancellationToken ct);
}
// END-FEAT::BE-667::2026-03-17::AHL::Interfaz del gate de control manual de notificaciones
