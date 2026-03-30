// BEGIN-FEAT::BE-662::2026-03-17::AHL::Servicio de generación CUFE+JWT para PA
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public class CufeGenerationService : ICufeGenerationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CufeGenerationService> _logger;

    public CufeGenerationService(
        IHttpClientFactory httpClientFactory,
        ILogger<CufeGenerationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<CufeResult> GenerateCufeAsync(string xmlContent, CountryConfig config, CancellationToken ct)
    {
        // Generar CUFE basado en datos del documento
        var cufe = GenerateCufeHash(xmlContent, config);

        // Obtener JWT via GetJWT
        var jwt = await GetJwtAsync(config, ct);

        // Inyectar CUFE en XML
        var updatedXml = InjectCufe(xmlContent, cufe);

        _logger.LogDebug("CUFE generado para {Country}: {CufePrefix}...",
            config.CountryCode, cufe[..Math.Min(16, cufe.Length)]);

        return new CufeResult(cufe, jwt, updatedXml);
    }

    private static string GenerateCufeHash(string xmlContent, CountryConfig config)
    {
        var input = $"{config.CountryCode}|{config.TaxId}|{DateTimeOffset.UtcNow:yyyyMMdd}|{xmlContent.Length}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    private async Task<string> GetJwtAsync(CountryConfig config, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("NucClient");
        var payload = JsonSerializer.Serialize(new { taxId = config.TaxId, country = config.CountryCode });
        var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{config.NucLoginEndpoint}/getjwt", content, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("jwt").GetString()
            ?? throw new InvalidOperationException("JWT no encontrado en respuesta GetJWT");
    }

    private static string InjectCufe(string xmlContent, string cufe)
    {
        var doc = System.Xml.Linq.XDocument.Parse(xmlContent);
        var ns = doc.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;

        var cufeNode = doc.Descendants(ns + "CUFE").FirstOrDefault();
        if (cufeNode != null)
            cufeNode.Value = cufe;
        else
            doc.Root?.Add(new System.Xml.Linq.XElement(ns + "CUFE", cufe));

        return doc.ToString();
    }
}
// END-FEAT::BE-662::2026-03-17::AHL::Servicio de generación CUFE+JWT para PA
