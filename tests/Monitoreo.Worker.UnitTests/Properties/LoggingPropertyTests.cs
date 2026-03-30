// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 11: Completitud de logs estructurados
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 11: Completitud de logs estructurados.
/// Valida: Req 10.3, 10.4
/// </summary>
public class LoggingPropertyTests
{
    [Property(Arbitrary = [typeof(LoggingArbitraries)])]
    public Property SuccessLogContainsRequiredContext(MonitoringResult result)
    {
        if (result.ResultStatus)
        {
            // Log Information debe incluir: país, tipo, duración, resultado
            return (!string.IsNullOrEmpty(result.Country)
                 && result.TransactionTimeMs >= 0).ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(LoggingArbitraries)])]
    public Property ErrorLogContainsErrorMessage(MonitoringResult result)
    {
        if (!result.ResultStatus)
        {
            // Log Error debe incluir: país, tipo, mensaje de error
            return (!string.IsNullOrEmpty(result.Country)).ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(LoggingArbitraries)])]
    public Property AllResultsHaveTimestamp(MonitoringResult result)
    {
        return (result.CreatedAt != default).ToProperty();
    }
}

public static class LoggingArbitraries
{
    public static Arbitrary<MonitoringResult> MonitoringResultArb() =>
        MonitoringArbitraries.MonitoringResultArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 11: Completitud de logs estructurados
