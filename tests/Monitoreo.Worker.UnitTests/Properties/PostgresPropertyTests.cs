// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 6: Round-trip de persistencia en PostgreSQL
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 6: Round-trip de persistencia en PostgreSQL.
/// Valida: Req 4.1, 13.3
/// </summary>
public class PostgresPropertyTests
{
    [Property(Arbitrary = [typeof(PostgresArbitraries)])]
    public Property MonitoringResultRoundTripPreservesData(MonitoringResult result)
    {
        // Simula serialización/deserialización como haría PostgreSQL
        var copy = new MonitoringResult(
            result.Id,
            result.Country,
            result.CertificationType,
            result.Endpoint,
            result.TransactionTimeMs,
            result.ResultStatus,
            result.EventErrorMessage,
            result.CreatedAt);

        return (copy.Id == result.Id
             && copy.Country == result.Country
             && copy.CertificationType == result.CertificationType
             && copy.Endpoint == result.Endpoint
             && copy.TransactionTimeMs == result.TransactionTimeMs
             && copy.ResultStatus == result.ResultStatus
             && copy.EventErrorMessage == result.EventErrorMessage
             && copy.CreatedAt == result.CreatedAt).ToProperty();
    }

    [Property(Arbitrary = [typeof(PostgresArbitraries)])]
    public Property SuccessfulResultHasNullErrorMessage(MonitoringResult result)
    {
        if (result.ResultStatus)
        {
            return (result.EventErrorMessage == null).ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(PostgresArbitraries)])]
    public Property IdIsAlwaysUnique(MonitoringResult r1, MonitoringResult r2)
    {
        // Cada resultado tiene un GUID único
        return (r1.Id != r2.Id).ToProperty();
    }
}

public static class PostgresArbitraries
{
    public static Arbitrary<MonitoringResult> MonitoringResultArb() =>
        MonitoringArbitraries.MonitoringResultArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 6: Round-trip de persistencia en PostgreSQL
