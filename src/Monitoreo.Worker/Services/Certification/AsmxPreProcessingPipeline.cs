// BEGIN-FEAT::BE-662::2026-03-17::AHL::Pipeline de pre-procesamiento ASMX: PFX(PA,DO) → QR(PA) → CUFE+JWT(PA)
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public class AsmxPreProcessingPipeline : IAsmxPreProcessingPipeline
{
    private readonly IPfxSigningService _pfxSigning;
    private readonly IQrGenerationService _qrGeneration;
    private readonly ICufeGenerationService _cufeGeneration;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<AsmxPreProcessingPipeline> _logger;

    public AsmxPreProcessingPipeline(
        IPfxSigningService pfxSigning,
        IQrGenerationService qrGeneration,
        ICufeGenerationService cufeGeneration,
        IConfiguration configuration,
        IHostEnvironment environment,
        IAmazonSecretsManager secretsManager,
        ILogger<AsmxPreProcessingPipeline> logger)
    {
        _pfxSigning = pfxSigning;
        _qrGeneration = qrGeneration;
        _cufeGeneration = cufeGeneration;
        _configuration = configuration;
        _environment = environment;
        _secretsManager = secretsManager;
        _logger = logger;
    }

    public async Task<string> ProcessAsync(string xmlContent, CountryConfig config, CancellationToken ct)
    {
        var xml = xmlContent;

        // Orden PA (igual al monitoreo viejo): CUFE (dId) -> Firma PFX -> QR
        // El CUFE va ANTES de firmar porque dId es parte del contenido firmado.
        // El QR va DESPUES porque usa el DigestValue de la firma.

        // Paso 1: CUFE (PA) — calcula dId con el algoritmo de la DGI
        if (config.RequiresCufe)
        {
            _logger.LogDebug("Pipeline {Country}: Generando CUFE (dId)", config.CountryCode);
            var cufeResult = await _cufeGeneration.GenerateCufeAsync(xml, config, ct);
            xml = cufeResult.UpdatedXml;
        }

        // Paso 2: Firma PFX (PA, DO)
        if (config.RequiresPfxSignature)
        {
            _logger.LogDebug("Pipeline {Country}: Firmando XML con PFX", config.CountryCode);
            var pfxBase64 = GetPfxFromConfig(config.CountryCode, "PfxBase64")
                ?? await GetSecretSafeAsync(config.PfxSecretArn, ct);
            var pfxPassword = GetPfxFromConfig(config.CountryCode, "PfxPassword")
                ?? await GetSecretSafeAsync(config.PfxPasswordSecretArn, ct);

            if (string.IsNullOrEmpty(pfxBase64) || string.IsNullOrEmpty(pfxPassword))
            {
                _logger.LogWarning("Pipeline {Country}: PFX no disponible, omitiendo firma", config.CountryCode);
            }
            else
            {
                xml = await _pfxSigning.SignXmlAsync(xml, pfxBase64, pfxPassword, ct);
            }
        }

        // Paso 3: QR (PA) — usa el DigestValue de la firma
        if (config.RequiresQrGeneration)
        {
            _logger.LogDebug("Pipeline {Country}: Generando QR", config.CountryCode);
            xml = await _qrGeneration.AddQrToXmlAsync(xml, config, ct);
        }

        return xml;
    }

    private string? GetPfxFromConfig(string country, string key)
    {
        return _configuration[$"Secrets:{country}:{key}"];
    }

    private async Task<string?> GetSecretSafeAsync(string? secretArn, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(secretArn)) return null;
        try
        {
            var response = await _secretsManager.GetSecretValueAsync(
                new GetSecretValueRequest { SecretId = secretArn }, ct);
            return response.SecretString;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo obtener secreto {Arn}", secretArn);
            return null;
        }
    }
}
// END-FEAT::BE-662::2026-03-17::AHL::Pipeline de pre-procesamiento ASMX: PFX(PA,DO) → QR(PA) → CUFE+JWT(PA)
