// BEGIN-FEAT::BE-668::2026-03-17::AHL::Proveedor de configuración AWS SSM + Secrets Manager con fallback a appsettings para desarrollo local
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Configuration;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Configuration;

public class AwsConfigurationProvider : IConfigurationProvider
{
    private readonly IAmazonSimpleSystemsManagement _ssm;
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AwsConfigurationProvider> _logger;

    public AwsConfigurationProvider(
        IAmazonSimpleSystemsManagement ssm,
        IAmazonSecretsManager secretsManager,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<AwsConfigurationProvider> logger)
    {
        _ssm = ssm;
        _secretsManager = secretsManager;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CountryConfig>> LoadAllCountriesAsync(CancellationToken ct)
    {
        var enabledCountries = _configuration.GetSection("Monitoring:EnabledCountries").Get<string[]>()
            ?? ["GT", "SV", "DO", "CR", "PA"];

        var configs = new List<CountryConfig>();
        foreach (var country in enabledCountries)
        {
            try
            {
                var config = await LoadCountryAsync(country, ct);
                configs.Add(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando configuración para {Country}. Se omite del ciclo", country);
            }
        }

        return configs.AsReadOnly();
    }

    public async Task<CountryConfig> LoadCountryAsync(string countryCode, CancellationToken ct)
    {
        var env = _configuration["Monitoring:Environment"] ?? "Development";

        try
        {
            return await LoadFromSsmAsync(countryCode, env, ct);
        }
        catch (Exception ex) when (_environment.IsDevelopment())
        {
            _logger.LogWarning(ex, "SSM no disponible para {Country}, usando fallback a appsettings.{Country}.json", countryCode, countryCode);
            return LoadFromAppSettings(countryCode);
        }
    }

    private async Task<CountryConfig> LoadFromSsmAsync(string countryCode, string env, CancellationToken ct)
    {
        var prefix = $"/monitoreo/{env}/{countryCode}/";

        var response = await _ssm.GetParametersByPathAsync(new GetParametersByPathRequest
        {
            Path = prefix,
            Recursive = true,
            WithDecryption = true
        }, ct);

        var parameters = response.Parameters.ToDictionary(
            p => p.Name.Replace(prefix, ""),
            p => p.Value);

        string GetParam(string key) =>
            parameters.TryGetValue(key, out var val) ? val : string.Empty;

        bool GetBoolParam(string key) =>
            parameters.TryGetValue(key, out var val) && bool.TryParse(val, out var b) && b;

        int GetIntParam(string key, int defaultValue) =>
            parameters.TryGetValue(key, out var val) && int.TryParse(val, out var i) ? i : defaultValue;

        return new CountryConfig
        {
            CountryCode = countryCode,
            Enabled = GetBoolParam("enabled"),
            MonitoringIntervalSeconds = GetIntParam("monitoring-interval", 300),
            AlertThresholdMs = GetIntParam("alert-threshold-ms", 30000),
            AsmxEndpoint = GetParam("asmx-endpoint"),
            NucLoginEndpoint = GetParam("nuc-login-endpoint"),
            NucCertEndpoint = GetParam("nuc-cert-endpoint"),
            AsmxTemplatePath = GetParam("asmx-template-path"),
            NucTemplatePath = GetParam("nuc-template-path"),
            TaxId = GetParam("tax-id"),
            Requestor = GetParam("requestor"),
            NucUsername = GetParam("nuc-username"),
            NucAuthMode = GetParam("nuc-auth-mode"),
            NucUsernameFormat = GetParam("nuc-username-format"),
            NucCredentialSecretArn = GetParam("nuc-credential-secret-arn"),
            RequiresPfxSignature = GetBoolParam("requires-pfx-signature"),
            PfxSecretArn = GetParam("pfx-secret-arn"),
            PfxPasswordSecretArn = GetParam("pfx-password-secret-arn"),
            RequiresQrGeneration = GetBoolParam("requires-qr-generation"),
            QrCode = GetParam("qr-code"),
            RequiresCufe = GetBoolParam("requires-cufe"),
            EmailRecipients = GetParam("email-recipients").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            WhatsAppNumbers = GetParam("whatsapp-numbers").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            NotificationsEmailEnabled = GetBoolParam("notifications-email-enabled"),
            NotificationsWhatsAppEnabled = GetBoolParam("notifications-whatsapp-enabled"),
            NotificationCooldownMinutes = GetIntParam("notification-cooldown-minutes", 15),
            WhatsAppTokenSecretArn = GetParam("whatsapp-token-secret-arn")
        };
    }

    private CountryConfig LoadFromAppSettings(string countryCode)
    {
        var section = _configuration.GetSection($"Countries:{countryCode}");
        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"No se encontró configuración para {countryCode} en appsettings ni en SSM");
        }

        var config = new CountryConfig
        {
            CountryCode = section["CountryCode"] ?? countryCode,
            Enabled = section.GetValue<bool>("Enabled"),
            MonitoringIntervalSeconds = section.GetValue("MonitoringIntervalSeconds", 300),
            AlertThresholdMs = section.GetValue("AlertThresholdMs", 30000),
            AsmxEndpoint = section["AsmxEndpoint"] ?? string.Empty,
            NucLoginEndpoint = section["NucLoginEndpoint"] ?? string.Empty,
            NucCertEndpoint = section["NucCertEndpoint"] ?? string.Empty,
            AsmxTemplatePath = section["AsmxTemplatePath"] ?? $"Templates/{countryCode}/asmx-template.xml",
            NucTemplatePath = section["NucTemplatePath"] ?? $"Templates/{countryCode}/nuc-template.xml",
            TaxId = section["TaxId"] ?? string.Empty,
            Requestor = section["Requestor"] ?? string.Empty,
            NucUsername = section["NucUsername"] ?? string.Empty,
            NucAuthMode = section["NucAuthMode"] ?? "dynamic",
            NucUsernameFormat = section["NucUsernameFormat"],
            NucCredentialSecretArn = section["NucCredentialSecretArn"],
            RequiresPfxSignature = section.GetValue<bool>("RequiresPfxSignature"),
            PfxSecretArn = section["PfxSecretArn"],
            PfxPasswordSecretArn = section["PfxPasswordSecretArn"],
            RequiresQrGeneration = section.GetValue<bool>("RequiresQrGeneration"),
            QrCode = section["QrCode"],
            RequiresCufe = section.GetValue<bool>("RequiresCufe"),
            ApiEnabled = section.GetValue<bool>("ApiEnabled"),
            ApiEndpoint = section["ApiEndpoint"],
            ApiLoginEndpoint = section["ApiLoginEndpoint"],
            ApiTransactionType = section["ApiTransactionType"],
            ApiUsernameParam = section["ApiUsernameParam"],
            ApiResponseFormat = section["ApiResponseFormat"],
            AsmxTransactionType = section["AsmxTransactionType"] ?? "CERTIFICATE_FE",
            AsmxUsernameFormat = section["AsmxUsernameFormat"],
            EmailRecipients = section.GetSection("EmailRecipients").Get<List<string>>() ?? [],
            WhatsAppNumbers = section.GetSection("WhatsAppNumbers").Get<List<string>>() ?? [],
            NotificationsEmailEnabled = section.GetValue("NotificationsEmailEnabled", true),
            NotificationsWhatsAppEnabled = section.GetValue("NotificationsWhatsAppEnabled", true),
            NotificationCooldownMinutes = section.GetValue("NotificationCooldownMinutes", 15),
            WhatsAppTokenSecretArn = section["WhatsAppTokenSecretArn"]
        };

        ValidateConfig(config);
        return config;
    }

    private void ValidateConfig(CountryConfig config)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.CountryCode))
            errors.Add("CountryCode es obligatorio");
        if (string.IsNullOrWhiteSpace(config.AsmxEndpoint))
            errors.Add($"AsmxEndpoint es obligatorio para {config.CountryCode}");
        if (string.IsNullOrWhiteSpace(config.NucCertEndpoint))
            errors.Add($"NucCertEndpoint es obligatorio para {config.CountryCode}");
        if (config.RequiresPfxSignature && string.IsNullOrWhiteSpace(config.PfxSecretArn))
            errors.Add($"PfxSecretArn es obligatorio cuando RequiresPfxSignature=true ({config.CountryCode})");
        if (config.RequiresPfxSignature && string.IsNullOrWhiteSpace(config.PfxPasswordSecretArn))
            errors.Add($"PfxPasswordSecretArn es obligatorio cuando RequiresPfxSignature=true ({config.CountryCode})");

        if (errors.Count > 0)
        {
            _logger.LogWarning("Validación de configuración para {Country}: {Errors}",
                config.CountryCode, string.Join("; ", errors));
        }
    }
}
// END-FEAT::BE-668::2026-03-17::AHL::Proveedor de configuración AWS SSM + Secrets Manager con fallback a appsettings para desarrollo local
