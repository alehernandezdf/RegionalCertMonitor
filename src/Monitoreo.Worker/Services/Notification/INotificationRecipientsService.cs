// BEGIN-FEAT::BE-672::2026-07-20::AHL::Destinatarios de notificaciones desde BD (tabla notification_recipients)
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Notification;

public interface INotificationRecipientsService
{
    /// <summary>
    /// Devuelve los destinatarios activos para un país y canal desde la tabla notification_recipients.
    /// country '*' en la tabla aplica a todos los países. Devuelve lista vacía si no hay filas o hay error
    /// (el llamador decide el fallback a configuración).
    /// </summary>
    Task<IReadOnlyList<string>> GetRecipientsAsync(string countryCode, NotificationChannel channel, CancellationToken ct);
}
// END-FEAT::BE-672::2026-07-20::AHL::Destinatarios de notificaciones desde BD
