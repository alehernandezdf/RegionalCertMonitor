// BEGIN-TEST::BE-663::2026-03-17::AHL::Tests unitarios para NucCertificationService: BuildNucUsername, modos static/dynamic
using FluentAssertions;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Certification;

namespace Monitoreo.Worker.UnitTests.Services;

public class NucCertificationServiceTests
{
    private static CountryConfig MakeConfig(string authMode, string? format = null) => new()
    {
        CountryCode = "CR", Enabled = true,
        AsmxEndpoint = "https://test", NucLoginEndpoint = "https://test/login",
        NucCertEndpoint = "https://test/cert", AsmxTemplatePath = "t.xml",
        NucTemplatePath = "t.xml", TaxId = "3101234567",
        Requestor = "R", NucUsername = "admin",
        NucAuthMode = authMode, NucUsernameFormat = format,
        NucCredentialSecretArn = "arn:aws:secretsmanager:us-east-1:000:secret:nuc"
    };

    [Fact]
    public void BuildNucUsername_DynamicMode_InterpolatesFormat()
    {
        var config = MakeConfig("dynamic", "{Country}_{TaxId}_{NucUsername}");
        var result = NucCertificationService.BuildNucUsername(config);
        result.Should().Be("CR_3101234567_admin");
    }

    [Fact]
    public void BuildNucUsername_NullFormat_UsesDefault()
    {
        var config = MakeConfig("dynamic", null);
        var result = NucCertificationService.BuildNucUsername(config);
        result.Should().Be("CR.3101234567.admin");
    }

    [Fact]
    public void BuildNucUsername_SvFormat_InterpolatesNrcNit()
    {
        var config = MakeConfig("dynamic", "{NIT}-{NRC}") with { CountryCode = "SV", TaxId = "06141234567890" };
        var result = NucCertificationService.BuildNucUsername(config);
        result.Should().Be("06141234567890-06141234567890");
    }

    [Fact]
    public void BuildNucUsername_GtStatic_StillBuildsUsername()
    {
        var config = MakeConfig("static", "{Country}.{TaxId}") with { CountryCode = "GT" };
        var result = NucCertificationService.BuildNucUsername(config);
        result.Should().Be("GT.3101234567");
    }
}
// END-TEST::BE-663::2026-03-17::AHL::Tests unitarios para NucCertificationService: BuildNucUsername, modos static/dynamic
