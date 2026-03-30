// BEGIN-FEAT::BE-669::2026-03-17::AHL::Record de configuración multi-país con todos los campos de monitoreo, notificación, PFX, QR, CUFE y NUC auth
namespace Monitoreo.Worker.Models;

public record CountryConfig
{
    public required string CountryCode { get; init; }
    public bool Enabled { get; init; }
    public int MonitoringIntervalSeconds { get; init; } = 300;
    public int AlertThresholdMs { get; init; } = 30000;

    // Endpoints
    public required string AsmxEndpoint { get; init; }
    public required string NucLoginEndpoint { get; init; }
    public required string NucCertEndpoint { get; init; }

    // Templates
    public required string AsmxTemplatePath { get; init; }
    public required string NucTemplatePath { get; init; }

    // Identity
    public required string TaxId { get; init; }
    public required string Requestor { get; init; }
    public required string NucUsername { get; init; }

    // NUC Authentication
    public required string NucAuthMode { get; init; } // "dynamic" | "static"
    public string? NucUsernameFormat { get; init; }
    public string? NucCredentialSecretArn { get; init; }

    // PFX Signing (PA, DO)
    public bool RequiresPfxSignature { get; init; }
    public string? PfxSecretArn { get; init; }
    public string? PfxPasswordSecretArn { get; init; }

    // QR Generation (PA)
    public bool RequiresQrGeneration { get; init; }
    public string? QrCode { get; init; }

    // CUFE Generation (PA)
    public bool RequiresCufe { get; init; }

    // Notifications — Email
    public IReadOnlyList<string> EmailRecipients { get; init; } = [];
    public bool NotificationsEmailEnabled { get; init; } = true;

    // Notifications — WhatsApp
    public IReadOnlyList<string> WhatsAppNumbers { get; init; } = [];
    public bool NotificationsWhatsAppEnabled { get; init; } = true;
    public string? WhatsAppTokenSecretArn { get; init; }

    // Notification cooldown
    public int NotificationCooldownMinutes { get; init; } = 15;
}
// END-FEAT::BE-669::2026-03-17::AHL::Record de configuración multi-país con todos los campos de monitoreo, notificación, PFX, QR, CUFE y NUC auth
