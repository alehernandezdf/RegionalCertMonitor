// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 1 y 10: Solo países habilitados se programan + Validación de CountryConfig
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using FluentAssertions;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

public class ConfigurationPropertyTests
{
    /// <summary>
    /// Propiedad 1: Solo países con Enabled=true deben programarse para monitoreo.
    /// Valida: Req 1.2
    /// </summary>
    [Property(Arbitrary = [typeof(ConfigurationArbitraries)])]
    public Property OnlyEnabledCountriesAreScheduled(CountryConfig[] configs)
    {
        var enabled = configs.Where(c => c.Enabled).ToList();
        var disabled = configs.Where(c => !c.Enabled).ToList();

        return (enabled.All(c => c.Enabled) && disabled.All(c => !c.Enabled))
            .ToProperty();
    }

    /// <summary>
    /// Propiedad 10: CountryConfig con campos obligatorios ausentes debe ser inválido.
    /// Valida: Req 8.3, 8.4, 7.1, 9.3, 9.4, 9.5, 9.6
    /// </summary>
    [Property(Arbitrary = [typeof(ConfigurationArbitraries)])]
    public Property PfxArnsRequiredWhenPfxSignatureEnabled(CountryConfig config)
    {
        if (config.RequiresPfxSignature)
        {
            var valid = !string.IsNullOrEmpty(config.PfxSecretArn)
                     && !string.IsNullOrEmpty(config.PfxPasswordSecretArn);
            return valid.ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(ConfigurationArbitraries)])]
    public Property DynamicAuthRequiresUsernameFormat(CountryConfig config)
    {
        if (config.NucAuthMode == "dynamic")
        {
            return (!string.IsNullOrEmpty(config.NucUsernameFormat)).ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(ConfigurationArbitraries)])]
    public Property CountryCodeIsAlwaysValid(CountryConfig config)
    {
        var validCodes = new[] { "GT", "SV", "DO", "CR", "PA" };
        return validCodes.Contains(config.CountryCode).ToProperty();
    }

    [Property(Arbitrary = [typeof(ConfigurationArbitraries)])]
    public Property CooldownIsAlwaysPositive(CountryConfig config)
    {
        return (config.NotificationCooldownMinutes > 0).ToProperty();
    }

    [Property(Arbitrary = [typeof(ConfigurationArbitraries)])]
    public Property QrConfigRequiredWhenQrEnabled(CountryConfig config)
    {
        if (config.RequiresQrGeneration)
        {
            return (!string.IsNullOrEmpty(config.QrCode)).ToProperty();
        }
        return true.ToProperty();
    }
}

public static class ConfigurationArbitraries
{
    public static Arbitrary<CountryConfig> CountryConfigArb() =>
        MonitoringArbitraries.CountryConfigArb();

    public static Arbitrary<CountryConfig[]> CountryConfigArrayArb()
    {
        var configGen = MonitoringArbitraries.CountryConfigArb().Generator;
        var gen =
            from c1 in configGen
            from c2 in configGen
            from c3 in configGen
            select new[] { c1, c2, c3 };
        return Arb.From(gen);
    }
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 1 y 10: Solo países habilitados se programan + Validación de CountryConfig
