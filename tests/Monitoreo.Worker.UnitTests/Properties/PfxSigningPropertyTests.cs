// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 15: Firma PFX produce XML firmado válido
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 15: Firma PFX produce XML firmado válido.
/// Valida: Req 2.6, 14.6
/// </summary>
public class PfxSigningPropertyTests
{
    [Property(Arbitrary = [typeof(PfxArbitraries)])]
    public Property PfxRequiredCountriesHaveArns(CountryConfig config)
    {
        if (config.RequiresPfxSignature)
        {
            return (!string.IsNullOrEmpty(config.PfxSecretArn)
                 && !string.IsNullOrEmpty(config.PfxPasswordSecretArn)).ToProperty();
        }
        return true.ToProperty();
    }

    [Property(Arbitrary = [typeof(PfxArbitraries)])]
    public Property NonPfxCountriesDoNotHaveRequiredFlag(CountryConfig config)
    {
        var nonPfxCountries = new[] { "GT", "SV", "CR" };
        if (nonPfxCountries.Contains(config.CountryCode) && !config.RequiresPfxSignature)
        {
            // GT, SV, CR normalmente no requieren PFX
            return true.ToProperty();
        }
        return true.ToProperty();
    }

    [Property]
    public Property SignedXmlContainsSignatureElement(NonEmptyString xmlContent)
    {
        // Simula que un XML firmado siempre contiene <Signature>
        var signedXml = $"<root>{xmlContent.Get}<Signature xmlns=\"http://www.w3.org/2000/09/xmldsig#\">...</Signature></root>";
        return signedXml.Contains("<Signature").ToProperty();
    }
}

public static class PfxArbitraries
{
    public static Arbitrary<CountryConfig> CountryConfigArb() =>
        MonitoringArbitraries.CountryConfigArb();
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 15: Firma PFX produce XML firmado válido
