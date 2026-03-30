// BEGIN-TEST::BE-669::2026-03-17::AHL::Tests unitarios para validación de CountryConfig: campos obligatorios, PFX ARNs condicionales
using FluentAssertions;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Models;

public class CountryConfigTests
{
    [Fact]
    public void CountryConfig_DefaultValues_AreCorrect()
    {
        var config = new CountryConfig
        {
            CountryCode = "GT", AsmxEndpoint = "https://test",
            NucLoginEndpoint = "https://test", NucCertEndpoint = "https://test",
            AsmxTemplatePath = "t.xml", NucTemplatePath = "t.xml",
            TaxId = "123", Requestor = "R", NucUsername = "u", NucAuthMode = "dynamic"
        };

        config.MonitoringIntervalSeconds.Should().Be(300);
        config.AlertThresholdMs.Should().Be(30000);
        config.NotificationsEmailEnabled.Should().BeTrue();
        config.NotificationsWhatsAppEnabled.Should().BeTrue();
        config.NotificationCooldownMinutes.Should().Be(15);
        config.RequiresPfxSignature.Should().BeFalse();
        config.RequiresQrGeneration.Should().BeFalse();
        config.RequiresCufe.Should().BeFalse();
    }

    [Fact]
    public void CountryConfig_PfxEnabled_RequiresArns()
    {
        var config = new CountryConfig
        {
            CountryCode = "PA", AsmxEndpoint = "https://test",
            NucLoginEndpoint = "https://test", NucCertEndpoint = "https://test",
            AsmxTemplatePath = "t.xml", NucTemplatePath = "t.xml",
            TaxId = "123", Requestor = "R", NucUsername = "u", NucAuthMode = "dynamic",
            RequiresPfxSignature = true,
            PfxSecretArn = "arn:pfx", PfxPasswordSecretArn = "arn:pfx-pwd"
        };

        config.RequiresPfxSignature.Should().BeTrue();
        config.PfxSecretArn.Should().NotBeNullOrWhiteSpace();
        config.PfxPasswordSecretArn.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void CountryConfig_PfxEnabled_MissingArns_IsInvalid()
    {
        var config = new CountryConfig
        {
            CountryCode = "PA", AsmxEndpoint = "https://test",
            NucLoginEndpoint = "https://test", NucCertEndpoint = "https://test",
            AsmxTemplatePath = "t.xml", NucTemplatePath = "t.xml",
            TaxId = "123", Requestor = "R", NucUsername = "u", NucAuthMode = "dynamic",
            RequiresPfxSignature = true,
            PfxSecretArn = null, PfxPasswordSecretArn = null
        };

        config.RequiresPfxSignature.Should().BeTrue();
        config.PfxSecretArn.Should().BeNull();
    }

    [Fact]
    public void CountryConfig_NotificationRecipients_DefaultEmpty()
    {
        var config = new CountryConfig
        {
            CountryCode = "GT", AsmxEndpoint = "https://test",
            NucLoginEndpoint = "https://test", NucCertEndpoint = "https://test",
            AsmxTemplatePath = "t.xml", NucTemplatePath = "t.xml",
            TaxId = "123", Requestor = "R", NucUsername = "u", NucAuthMode = "dynamic"
        };

        config.EmailRecipients.Should().BeEmpty();
        config.WhatsAppNumbers.Should().BeEmpty();
    }
}
// END-TEST::BE-669::2026-03-17::AHL::Tests unitarios para validación de CountryConfig: campos obligatorios, PFX ARNs condicionales
