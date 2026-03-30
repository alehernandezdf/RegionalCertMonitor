// BEGIN-TEST::BE-660::2026-03-17::AHL::Generadores FsCheck custom para MonitoringResult y CountryConfig
using FsCheck;
using FsCheck.Fluent;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests;

public static class MonitoringArbitraries
{
    private static readonly string[] Countries = ["GT", "SV", "DO", "CR", "PA"];
    private static readonly string[] AuthModes = ["dynamic", "static"];

    public static Arbitrary<MonitoringResult> MonitoringResultArb()
    {
        var gen =
            from country in Gen.Elements(Countries)
            from certType in Gen.Elements(new[] { CertificationType.ASMX, CertificationType.NUC })
            from timeMs in Gen.Choose(50, 60000)
            from status in Gen.Elements(new[] { true, false })
            from error in Gen.Elements(new string?[] { null, "Timeout", "Connection refused", "500 Internal Server Error" })
            select new MonitoringResult(
                Guid.NewGuid(), country, certType,
                $"https://api.example.com/{country.ToLower()}/cert",
                timeMs, status, status ? null : error, DateTimeOffset.UtcNow);

        return Arb.From(gen);
    }

    public static Arbitrary<CountryConfig> CountryConfigArb()
    {
        var gen =
            from country in Gen.Elements(Countries)
            from enabled in Gen.Elements(new[] { true, false })
            from interval in Gen.Choose(60, 600)
            from threshold in Gen.Choose(5000, 60000)
            from authMode in Gen.Elements(AuthModes)
            from requiresPfx in Gen.Elements(new[] { true, false })
            from requiresQr in Gen.Elements(new[] { true, false })
            from requiresCufe in Gen.Elements(new[] { true, false })
            from emailEnabled in Gen.Elements(new[] { true, false })
            from whatsAppEnabled in Gen.Elements(new[] { true, false })
            from cooldown in Gen.Choose(5, 60)
            select new CountryConfig
            {
                CountryCode = country,
                Enabled = enabled,
                MonitoringIntervalSeconds = interval,
                AlertThresholdMs = threshold,
                AsmxEndpoint = $"https://asmx.example.com/{country.ToLower()}",
                NucLoginEndpoint = $"https://nuc.example.com/{country.ToLower()}/login",
                NucCertEndpoint = $"https://nuc.example.com/{country.ToLower()}/cert",
                AsmxTemplatePath = $"Templates/{country}/asmx-template.xml",
                NucTemplatePath = $"Templates/{country}/nuc-template.xml",
                TaxId = "123456789",
                Requestor = "TEST_REQUESTOR",
                NucUsername = "test_user",
                NucAuthMode = authMode,
                NucUsernameFormat = authMode == "dynamic" ? "{Country}_{TaxId}_{NucUsername}" : null,
                NucCredentialSecretArn = "arn:aws:secretsmanager:us-east-1:000000:secret:test",
                RequiresPfxSignature = requiresPfx,
                PfxSecretArn = requiresPfx ? "arn:aws:secretsmanager:us-east-1:000000:secret:pfx" : null,
                PfxPasswordSecretArn = requiresPfx ? "arn:aws:secretsmanager:us-east-1:000000:secret:pfx-pwd" : null,
                RequiresQrGeneration = requiresQr,
                QrCode = requiresQr ? "QR_CODE_VALUE" : null,
                RequiresCufe = requiresCufe,
                EmailRecipients = ["test@example.com"],
                NotificationsEmailEnabled = emailEnabled,
                WhatsAppNumbers = ["+50212345678"],
                NotificationsWhatsAppEnabled = whatsAppEnabled,
                WhatsAppTokenSecretArn = "arn:aws:secretsmanager:us-east-1:000000:secret:wa-token",
                NotificationCooldownMinutes = cooldown
            };

        return Arb.From(gen);
    }
}
// END-TEST::BE-660::2026-03-17::AHL::Generadores FsCheck custom para MonitoringResult y CountryConfig
