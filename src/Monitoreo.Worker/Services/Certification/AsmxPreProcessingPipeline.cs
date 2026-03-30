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
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly ILogger<AsmxPreProcessingPipeline> _logger;

    public AsmxPreProcessingPipeline(
        IPfxSigningService pfxSigning,
        IQrGenerationService qrGeneration,
        ICufeGenerationService cufeGeneration,
        IAmazonSecretsManager secretsManager,
        ILogger<AsmxPreProcessingPipeline> logger)
    {
        _pfxSigning = pfxSigning;
        _qrGeneration = qrGeneration;
        _cufeGeneration = cufeGeneration;
        _secretsManager = secretsManager;
        _logger = logger;
    }

    public async Task<string> ProcessAsync(string xmlContent, CountryConfig config, CancellationToken ct)
    {
        var xml = xmlContent;

        // Paso 1: Firma PFX (PA, DO)
        if (config.RequiresPfxSignature)
        {
            _logger.LogDebug("Pipeline {Country}: Firmando XML con PFX", config.CountryCode);
            var pfxBase64 = await GetSecretAsync(config.PfxSecretArn!, ct);
            var pfxPassword = await GetSecretAsync(config.PfxPasswordSecretArn!, ct);
            xml = await _pfxSigning.SignXmlAsync(xml, pfxBase64, pfxPassword, ct);
        }

        // Paso 2: Generación QR (PA)
        if (config.RequiresQrGeneration)
        {
            _logger.LogDebug("Pipeline {Country}: Generando QR", config.CountryCode);
            xml = await _qrGeneration.AddQrToXmlAsync(xml, config, ct);
        }

        // Paso 3: CUFE + JWT (PA)
        if (config.RequiresCufe)
        {
            _logger.LogDebug("Pipeline {Country}: Generando CUFE + JWT", config.CountryCode);
            var cufeResult = await _cufeGeneration.GenerateCufeAsync(xml, config, ct);
            xml = cufeResult.UpdatedXml;
        }

        return xml;
    }

    private async Task<string> GetSecretAsync(string secretArn, CancellationToken ct)
    {
        var response = await _secretsManager.GetSecretValueAsync(
            new GetSecretValueRequest { SecretId = secretArn }, ct);
        return response.SecretString;
    }
}
// END-FEAT::BE-662::2026-03-17::AHL::Pipeline de pre-procesamiento ASMX: PFX(PA,DO) → QR(PA) → CUFE+JWT(PA)
