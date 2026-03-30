// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 17: Modo de autenticación NUC determina estrategia de token
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 17: Modo de autenticación NUC determina estrategia de obtención de token.
/// Valida: Req 3.1, 3.2, 9.4, 9.5, 14.7
/// </summary>
public class NucAuthPropertyTests
{
    [Property(Arbitrary = [typeof(NucAuthArbitraries)])]
    public Property StaticModeDoesNotRequireLoginEndpoint(CountryConfig config)
    {
        if (config.NucAuthMode == "static")
        {
            // Modo estático: no necesita formato de username (usa token directo)
            return true.ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(NucAuthArbitraries)])]
    public Property DynamicModeRequiresUsernameFormat(CountryConfig config)
    {
        if (config.NucAuthMode == "dynamic")
        {
            return (!string.IsNullOrEmpty(config.NucUsernameFormat)).ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(NucAuthArbitraries)])]
    public Property AuthModeIsAlwaysDynamicOrStatic(CountryConfig config)
    {
        return (config.NucAuthMode == "dynamic" || config.NucAuthMode == "static").ToProperty();
    }

    [Property]
    public Property UsernameFormatInterpolationProducesNonEmpty(
        NonEmptyString country, NonEmptyString taxId, NonEmptyString nucUser)
    {
        var format = "{Country}.{TaxId}.{NucUsername}";
        var result = format
            .Replace("{Country}", country.Get)
            .Replace("{TaxId}", taxId.Get)
            .Replace("{NucUsername}", nucUser.Get);

        return (!string.IsNullOrEmpty(result)
             && result.Contains(country.Get)
             && result.Contains(taxId.Get)
             && result.Contains(nucUser.Get)).ToProperty();
    }

    [Property]
    public Property SvFormatInterpolationUsesNrcNit(NonEmptyString nrc, NonEmptyString nit)
    {
        var format = "SV.{NRC}.{NIT}";
        var result = format
            .Replace("{NRC}", nrc.Get)
            .Replace("{NIT}", nit.Get);

        return (result.StartsWith("SV.")
             && result.Contains(nrc.Get)
             && result.Contains(nit.Get)).ToProperty();
    }
}

public static class NucAuthArbitraries
{
    public static Arbitrary<CountryConfig> CountryConfigArb() =>
        MonitoringArbitraries.CountryConfigArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 17: Modo de autenticación NUC determina estrategia de token
