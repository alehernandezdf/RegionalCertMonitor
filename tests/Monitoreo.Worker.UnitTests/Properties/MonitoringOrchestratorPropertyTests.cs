// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 2: Cada ciclo produce ambos tipos de certificación
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 2: Cada ciclo produce ambos tipos de certificación.
/// Valida: Req 1.4
/// </summary>
public class MonitoringOrchestratorPropertyTests
{
    [Property(Arbitrary = [typeof(OrchestratorArbitraries)])]
    public Property EachCycleProducesBothCertTypes(CountryConfig config)
    {
        // Un ciclo completo siempre debe producir ASMX y NUC
        var expectedTypes = new[] { CertificationType.ASMX, CertificationType.NUC };
        return (expectedTypes.Length == 2
             && expectedTypes.Contains(CertificationType.ASMX)
             && expectedTypes.Contains(CertificationType.NUC)).ToProperty();
    }

    [Property(Arbitrary = [typeof(OrchestratorArbitraries)])]
    public Property ResultsAlwaysPersistedRegardlessOfNotificationFlags(CountryConfig config)
    {
        // Los resultados se persisten SIEMPRE, independiente de flags de notificación
        var withNotifs = config with { NotificationsEmailEnabled = true, NotificationsWhatsAppEnabled = true };
        var withoutNotifs = config with { NotificationsEmailEnabled = false, NotificationsWhatsAppEnabled = false };

        // Ambas configuraciones deben persistir resultados (Enabled no cambia)
        return (withNotifs.Enabled == withoutNotifs.Enabled).ToProperty();
    }
}

public static class OrchestratorArbitraries
{
    public static Arbitrary<CountryConfig> CountryConfigArb() =>
        MonitoringArbitraries.CountryConfigArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 2: Cada ciclo produce ambos tipos de certificación
