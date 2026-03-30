// BEGIN-FEAT::BE-665::2026-03-17::AHL::Interfaz de servicio de notificación
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public interface INotificationService
{
    Task NotifyAsync(NotificationPayload payload, CancellationToken ct);
}
// END-FEAT::BE-665::2026-03-17::AHL::Interfaz de servicio de notificación
