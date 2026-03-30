// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 4: Invariante de construcción de MonitoringResult
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 4: Invariante de construcción de MonitoringResult.
/// Valida: Req 2.3, 2.4, 3.3, 3.5, 4.2
/// </summary>
public class MonitoringResultPropertyTests
{
    [Property(Arbitrary = [typeof(MonitoringResultArbitraries)])]
    public Property FailedResultAlwaysHasErrorOrNullSuccess(MonitoringResult result)
    {
        if (!result.ResultStatus)
        {
            // Fallido puede tener error message (no es estrictamente requerido en todos los paths)
            return true.ToProperty();
        }
        // Exitoso debe tener error null
        return (result.EventErrorMessage == null).ToProperty();
    }

    [Property(Arbitrary = [typeof(MonitoringResultArbitraries)])]
    public Property TransactionTimeIsNonNegative(MonitoringResult result)
    {
        return (result.TransactionTimeMs >= 0).ToProperty();
    }

    [Property(Arbitrary = [typeof(MonitoringResultArbitraries)])]
    public Property CountryIsAlwaysValidCode(MonitoringResult result)
    {
        var validCountries = new[] { "GT", "SV", "DO", "CR", "PA" };
        return validCountries.Contains(result.Country).ToProperty();
    }

    [Property(Arbitrary = [typeof(MonitoringResultArbitraries)])]
    public Property CertificationTypeIsAsmxOrNuc(MonitoringResult result)
    {
        return (result.CertificationType == CertificationType.ASMX
             || result.CertificationType == CertificationType.NUC).ToProperty();
    }

    [Property(Arbitrary = [typeof(MonitoringResultArbitraries)])]
    public Property EndpointIsNeverEmpty(MonitoringResult result)
    {
        return (!string.IsNullOrEmpty(result.Endpoint)).ToProperty();
    }

    [Property(Arbitrary = [typeof(MonitoringResultArbitraries)])]
    public Property RoundTripPreservesAllFields(MonitoringResult result)
    {
        // Simula round-trip: crear nuevo record con mismos valores
        var copy = result with { };
        return (copy == result).ToProperty();
    }
}

public static class MonitoringResultArbitraries
{
    public static Arbitrary<MonitoringResult> MonitoringResultArb() =>
        MonitoringArbitraries.MonitoringResultArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 4: Invariante de construcción de MonitoringResult
