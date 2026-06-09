// BEGIN-FEAT::BE-661::2026-03-30::AHL::Servicio API FEL con login dinamico y respuesta XML
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Persistence;

namespace Monitoreo.Worker.Services.Certification;

public class ApiFelCertificationService : ICertificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ISequentialCounterService _counterService;
    private readonly ILogger<ApiFelCertificationService> _logger;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public CertificationType Type => CertificationType.API;

    public ApiFelCertificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ISequentialCounterService counterService,
        ILogger<ApiFelCertificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _counterService = counterService;
        _logger = logger;
    }

    public async Task<MonitoringResult> CertifyAsync(CountryConfig config, CancellationToken ct)
    {
        if (!config.ApiEnabled || string.IsNullOrEmpty(config.ApiEndpoint))
        {
            return new MonitoringResult(Guid.NewGuid(), config.CountryCode, CertificationType.API,
                config.ApiEndpoint ?? "N/A", 0, false, "API no habilitada", DateTimeOffset.UtcNow);
        }

        var consecutivo = await _counterService.GetNextAsync(config.CountryCode, "API", ct);

        try
        {
            var token = await GetTokenAsync(config, ct);
            var templateXml = File.ReadAllText(config.AsmxTemplatePath);
            var xml = InjectDynamicFields(templateXml, config);

            var url = $"{config.ApiEndpoint}?NIT={config.TaxId}&TIPO={config.ApiTransactionType ?? "CERTIFICATE_DTE_XML_TOSIGN"}&FORMAT=XML&USERNAME={config.ApiUsernameParam ?? config.NucUsername}";

            var client = _httpClientFactory.CreateClient("NucClient");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(token);
            var content = new StringContent(xml, Encoding.UTF8, "application/xml");

            _logger.LogDebug("API {Country} #{Consecutivo} request: {Url}", config.CountryCode, consecutivo, url);

            var sw = Stopwatch.StartNew();
            var response = await client.PostAsync(url, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();

            var (success, errorMsg) = ParseXmlResponse(responseBody);

            _logger.LogInformation("API {Country} #{Consecutivo}: {Status} en {TimeMs}ms",
                config.CountryCode, consecutivo, success ? "OK" : "FAIL", sw.ElapsedMilliseconds);

            return new MonitoringResult(Guid.NewGuid(), config.CountryCode, CertificationType.API,
                config.ApiEndpoint, sw.ElapsedMilliseconds, success,
                success ? null : errorMsg, DateTimeOffset.UtcNow, RawResponse: responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API {Country} #{Consecutivo}: Error", config.CountryCode, consecutivo);
            return new MonitoringResult(Guid.NewGuid(), config.CountryCode, CertificationType.API,
                config.ApiEndpoint ?? "N/A", 0, false, ex.Message, DateTimeOffset.UtcNow, RawResponse: ex.ToString());
        }
    }

    private async Task<string> GetTokenAsync(CountryConfig config, CancellationToken ct)
    {
        if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        if (!string.IsNullOrEmpty(config.ApiLoginEndpoint))
        {
            var username = $"{config.CountryCode}.{config.TaxId}.{config.NucUsername}";
            var password = _configuration[$"Secrets:{config.CountryCode}:NucCredentialPassword"] ?? "";
            var client = _httpClientFactory.CreateClient("NucClient");
            var loginPayload = JsonSerializer.Serialize(new { Username = username, Password = password });
            var loginContent = new StringContent(loginPayload, Encoding.UTF8, "application/json");
            var loginResponse = await client.PostAsync(config.ApiLoginEndpoint, loginContent, ct);
            loginResponse.EnsureSuccessStatusCode();
            var loginBody = await loginResponse.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(loginBody);
            _cachedToken = doc.RootElement.TryGetProperty("Token", out var t) ? t.GetString()
                : doc.RootElement.TryGetProperty("token", out var t2) ? t2.GetString() : null;
            _tokenExpiry = DateTimeOffset.UtcNow.AddMinutes(5);
            return _cachedToken ?? throw new InvalidOperationException("Token no encontrado en respuesta API FEL login");
        }

        return _configuration[$"Secrets:{config.CountryCode}:NucStaticToken"] ?? "";
    }

    private static (bool success, string? error) ParseXmlResponse(string responseBody)
    {
        try
        {
            var doc = XDocument.Parse(responseBody);
            // Buscar Codigo ignorando namespace
            var codigo = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Codigo");
            if (codigo != null && codigo.Value == "1") return (true, null);
            var data = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ResponseDATA1");
            return (false, data?.Value ?? responseBody[..Math.Min(300, responseBody.Length)]);
        }
        catch
        {
            return (false, responseBody[..Math.Min(300, responseBody.Length)]);
        }
    }

    private static string InjectDynamicFields(string xml, CountryConfig config)
    {
        var doc = XDocument.Parse(xml);
        XNamespace dte = "http://www.sat.gob.gt/dte/fel/0.2.0";
        var datosGenerales = doc.Descendants(dte + "DatosGenerales").FirstOrDefault();
        if (datosGenerales != null)
            datosGenerales.SetAttributeValue("FechaHoraEmision", TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Guatemala")).ToString("yyyy-MM-ddTHH:mm:ss"));
        return doc.ToString();
    }
}
// END-FEAT::BE-661::2026-03-30::AHL::Servicio API FEL con login dinamico y respuesta XML
