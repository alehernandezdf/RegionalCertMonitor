// BEGIN-FEAT::BE-667::2026-03-17::AHL::Records de resultado del gate de notificaciones y canal
namespace Monitoreo.Worker.Models;

public record NotificationGateResult(bool IsAllowed, string? SuppressedReason);

public enum NotificationChannel
{
    Email,
    WhatsApp
}
// END-FEAT::BE-667::2026-03-17::AHL::Records de resultado del gate de notificaciones y canal
