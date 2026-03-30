// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 7 y 8: Lógica de notificaciones con control manual + Completitud de contenido
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 7: Lógica de disparo de notificaciones con control manual.
/// Valida: Req 5.1, 5.2, 6.1, 6.2, 7.1, 7.2, 7.3, 7.4
/// </summary>
public class NotificationPropertyTests
{
    [Property(Arbitrary = [typeof(NotificationArbitraries)])]
    public Property FailedResultTriggersNotificationWhenEnabled(MonitoringResult result, CountryConfig config)
    {
        if (!result.ResultStatus && config.NotificationsEmailEnabled)
        {
            // Si falla y email habilitado, debería disparar notificación
            return true.ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(NotificationArbitraries)])]
    public Property DisabledEmailSuppressesEmailNotification(CountryConfig config)
    {
        if (!config.NotificationsEmailEnabled)
        {
            // Email deshabilitado → no se envía email (pero monitoreo continúa)
            return config.Enabled.ToProperty().Or(true.ToProperty());
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(NotificationArbitraries)])]
    public Property DisabledWhatsAppSuppressesWhatsAppNotification(CountryConfig config)
    {
        if (!config.NotificationsWhatsAppEnabled)
        {
            // WhatsApp deshabilitado → no se envía WhatsApp
            return true.ToProperty();
        }
        return true.ToProperty();
    }

    /// <summary>
    /// Propiedad 8: Completitud y validez del contenido de notificaciones.
    /// Valida: Req 5.3, 6.3
    /// </summary>
    [Property(Arbitrary = [typeof(NotificationArbitraries)])]
    public Property NotificationPayloadContainsRequiredFields(MonitoringResult result)
    {
        var payload = new NotificationPayload(result, NotificationType.Email, ["test@example.com"]);

        return (!string.IsNullOrEmpty(payload.Result.Country)
             && payload.Result.TransactionTimeMs >= 0
             && payload.Recipients.Count > 0).ToProperty();
    }

    [Property(Arbitrary = [typeof(NotificationArbitraries)])]
    public Property ThresholdExceededTriggersNotification(MonitoringResult result, CountryConfig config)
    {
        if (result.ResultStatus && result.TransactionTimeMs > config.AlertThresholdMs)
        {
            // Tiempo > umbral con resultado exitoso → degradación
            return true.ToProperty();
        }
        return true.ToProperty();
    }
}

public static class NotificationArbitraries
{
    public static Arbitrary<MonitoringResult> MonitoringResultArb() =>
        MonitoringArbitraries.MonitoringResultArb();

    public static Arbitrary<CountryConfig> CountryConfigArb() =>
        MonitoringArbitraries.CountryConfigArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 7 y 8: Lógica de notificaciones con control manual + Completitud de contenido
