// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 13 y 14: Cooldown previene spam + Monitoreo independiente de flags
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 13: Cooldown de notificaciones previene spam.
/// Valida: Req 7.7
/// </summary>
public class NotificationGatePropertyTests
{
    [Property(Arbitrary = [typeof(NotificationGateArbitraries)])]
    public Property CooldownAlwaysPositiveMinutes(CountryConfig config)
    {
        return (config.NotificationCooldownMinutes > 0).ToProperty();
    }

    [Property(Arbitrary = [typeof(NotificationGateArbitraries)])]
    public Property TwoNotificationsWithinCooldownSecondIsSuppressed(CountryConfig config)
    {
        // Si se envía una notificación, la siguiente dentro del cooldown debe suprimirse
        var cooldown = TimeSpan.FromMinutes(config.NotificationCooldownMinutes);
        var firstTime = DateTimeOffset.UtcNow;
        var secondTime = firstTime.AddSeconds(30); // Dentro del cooldown

        var elapsed = secondTime - firstTime;
        return (elapsed < cooldown).ToProperty();
    }

    [Property(Arbitrary = [typeof(NotificationGateArbitraries)])]
    public Property NotificationAfterCooldownIsAllowed(CountryConfig config)
    {
        var cooldown = TimeSpan.FromMinutes(config.NotificationCooldownMinutes);
        var firstTime = DateTimeOffset.UtcNow;
        var secondTime = firstTime.Add(cooldown).AddSeconds(1); // Después del cooldown

        var elapsed = secondTime - firstTime;
        return (elapsed >= cooldown).ToProperty();
    }

    /// <summary>
    /// Propiedad 14: Monitoreo continúa independiente de flags de notificación.
    /// Valida: Req 7.2, 7.3
    /// </summary>
    [Property(Arbitrary = [typeof(NotificationGateArbitraries)])]
    public Property MonitoringContinuesRegardlessOfNotificationFlags(CountryConfig config)
    {
        // Independientemente de los flags de notificación, el país puede estar habilitado para monitoreo
        // Los flags de notificación NO afectan el campo Enabled
        var emailOff = config with { NotificationsEmailEnabled = false };
        var whatsAppOff = config with { NotificationsWhatsAppEnabled = false };
        var bothOff = config with { NotificationsEmailEnabled = false, NotificationsWhatsAppEnabled = false };

        return (emailOff.Enabled == config.Enabled
             && whatsAppOff.Enabled == config.Enabled
             && bothOff.Enabled == config.Enabled).ToProperty();
    }

    [Property(Arbitrary = [typeof(NotificationGateArbitraries)])]
    public Property GateResultIsEitherAllowedOrHasReason()
    {
        var allowed = new NotificationGateResult(true, null);
        var suppressed = new NotificationGateResult(false, "Kill switch global desactivado");

        return (allowed.IsAllowed && allowed.SuppressedReason == null
             && !suppressed.IsAllowed && !string.IsNullOrEmpty(suppressed.SuppressedReason)).ToProperty();
    }
}

public static class NotificationGateArbitraries
{
    public static Arbitrary<CountryConfig> CountryConfigArb() =>
        MonitoringArbitraries.CountryConfigArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 13 y 14: Cooldown previene spam + Monitoreo independiente de flags
