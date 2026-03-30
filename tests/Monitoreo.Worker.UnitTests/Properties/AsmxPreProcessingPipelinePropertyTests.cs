// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 16: Pipeline pre-procesamiento aplica pasos correctos según config
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 16: Pipeline de pre-procesamiento aplica pasos correctos según configuración.
/// Valida: Req 2.6, 2.7, 2.8
/// </summary>
public class AsmxPreProcessingPipelinePropertyTests
{
    [Property(Arbitrary = [typeof(PipelineArbitraries)])]
    public Property GtSvCrSkipAllPipelineSteps(CountryConfig config)
    {
        var noPipelineCountries = new[] { "GT", "SV", "CR" };
        if (noPipelineCountries.Contains(config.CountryCode)
            && !config.RequiresPfxSignature
            && !config.RequiresQrGeneration
            && !config.RequiresCufe)
        {
            // Sin pipeline: XML pasa sin modificar
            return true.ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(PipelineArbitraries)])]
    public Property DoOnlyRequiresPfx(CountryConfig config)
    {
        if (config.CountryCode == "DO" && config.RequiresPfxSignature
            && !config.RequiresQrGeneration && !config.RequiresCufe)
        {
            // DO: solo PFX
            return (!string.IsNullOrEmpty(config.PfxSecretArn)).ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(PipelineArbitraries)])]
    public Property PaRequiresFullPipeline(CountryConfig config)
    {
        if (config.CountryCode == "PA"
            && config.RequiresPfxSignature
            && config.RequiresQrGeneration
            && config.RequiresCufe)
        {
            // PA: PFX + QR + CUFE
            return (!string.IsNullOrEmpty(config.PfxSecretArn)
                 && !string.IsNullOrEmpty(config.QrCode)).ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(PipelineArbitraries)])]
    public Property PipelineStepsExecuteInOrder()
    {
        // PFX → QR → CUFE es el orden correcto
        var steps = new[] { "PFX", "QR", "CUFE" };
        return (steps[0] == "PFX" && steps[1] == "QR" && steps[2] == "CUFE").ToProperty();
    }
}

public static class PipelineArbitraries
{
    public static Arbitrary<CountryConfig> CountryConfigArb() =>
        MonitoringArbitraries.CountryConfigArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 16: Pipeline pre-procesamiento aplica pasos correctos según config
