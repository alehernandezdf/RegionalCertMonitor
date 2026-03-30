// BEGIN-FEAT::BE-665::2026-03-17::AHL::Records de payload y tipo de notificación
namespace Monitoreo.Worker.Models;

public record NotificationPayload(
    MonitoringResult Result,
    NotificationType Type,
    IReadOnlyList<string> Recipients);

public enum NotificationType
{
    Email,
    WhatsApp
}
// END-FEAT::BE-665::2026-03-17::AHL::Records de payload y tipo de notificación
