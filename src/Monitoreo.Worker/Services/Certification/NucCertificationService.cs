// BEGIN-FEAT::BE-663::2026-03-17::AHL::Servicio de certificación NUC REST con soporte dual de autenticación (dynamic/static) y consecutivo atómico
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Persistence;

namespace Monitoreo.Worker.Services.Certification;

public class NucCertificationService : ICertificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly IConfiguration _configuration;
    private readonly ISequentialCounterService _counterService;
    private readonly ILogger<NucCertificationService> _logger;

    // Cache de tokens por pais (token + expiracion)
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Token, DateTimeOffset Expiry)> _tokenCache = new();
    private static readonly TimeSpan TokenCacheDuration = TimeSpan.FromMinutes(10);

    // Cache de templates XML
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _templateCache = new();

    public CertificationType Type => CertificationType.NUC;

    public NucCertificationService(
        IHttpClientFactory httpClientFactory,
        IAmazonSecretsManager secretsManager,
        IConfiguration configuration,
        ISequentialCounterService counterService,
        ILogger<NucCertificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _secretsManager = secretsManager;
        _configuration = configuration;
        _counterService = counterService;
        _logger = logger;
    }

    public async Task<MonitoringResult> CertifyAsync(CountryConfig config, CancellationToken ct)
    {
        var consecutivo = await _counterService.GetNextAsync(config.CountryCode, "NUC", ct);

        try
        {
            // Obtener token (con cache)
            var token = config.NucAuthMode switch
            {
                "static" => await GetStaticTokenAsync(config, ct),
                "dynamic" => await GetCachedTokenAsync(config, ct),
                _ => throw new InvalidOperationException($"NucAuthMode invalido: {config.NucAuthMode}")
            };

            // Preparar XML (template cacheado en memoria)
            var templateXml = _templateCache.GetOrAdd(config.NucTemplatePath,
                path => File.ReadAllText(path));
            var xml = InjectNucDynamicFields(templateXml, config, consecutivo);

            // El tiempo se mide DENTRO de CertifyWithTokenAsync (solo PostAsync + ReadResponse, igual al monitoreo viejo)
            var response = await CertifyWithTokenAsync(config.NucCertEndpoint, token, xml, config, ct);

            _logger.LogInformation(
                "NUC {Country} #{Consecutivo} ({AuthMode}): {Status} en {TimeMs}ms",
                config.CountryCode, consecutivo, config.NucAuthMode,
                response.Success ? "OK" : "FAIL", response.ElapsedMs);

            return new MonitoringResult(
                Id: Guid.NewGuid(),
                Country: config.CountryCode,
                CertificationType: CertificationType.NUC,
                Endpoint: config.NucCertEndpoint,
                TransactionTimeMs: response.ElapsedMs,
                ResultStatus: response.Success,
                EventErrorMessage: response.Success ? null : response.ErrorMessage,
                CreatedAt: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NUC {Country} #{Consecutivo}: Error",
                config.CountryCode, consecutivo);

            return new MonitoringResult(
                Id: Guid.NewGuid(),
                Country: config.CountryCode,
                CertificationType: CertificationType.NUC,
                Endpoint: config.NucCertEndpoint,
                TransactionTimeMs: 0,
                ResultStatus: false,
                EventErrorMessage: ex.Message,
                CreatedAt: DateTimeOffset.UtcNow);
        }
    }

    private async Task<string> GetStaticTokenAsync(CountryConfig config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.NucCredentialSecretArn))
            throw new InvalidOperationException($"NucCredentialSecretArn requerido para modo static ({config.CountryCode})");

        var secret = await _secretsManager.GetSecretValueAsync(
            new GetSecretValueRequest { SecretId = config.NucCredentialSecretArn }, ct);
        return secret.SecretString;
    }

    // BEGIN-FIX::BE-660::2026-04-08::AHL::Circuit breaker de login NUC para evitar bloqueo de cuenta por intentos fallidos
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int FailCount, DateTimeOffset BlockedUntil)> _loginFailures = new();
    private const int MaxLoginFailures = 2;
    private static readonly TimeSpan LoginBlockDuration = TimeSpan.FromMinutes(30);

    private async Task<string> GetCachedTokenAsync(CountryConfig config, CancellationToken ct)
    {
        if (_tokenCache.TryGetValue(config.CountryCode, out var cached) && cached.Expiry > DateTimeOffset.UtcNow)
        {
            _logger.LogDebug("NUC {Country} usando token cacheado (expira en {Min:F0} min)",
                config.CountryCode, (cached.Expiry - DateTimeOffset.UtcNow).TotalMinutes);
            return cached.Token;
        }

        // Verificar si el login está bloqueado por intentos fallidos
        if (_loginFailures.TryGetValue(config.CountryCode, out var failure) && failure.BlockedUntil > DateTimeOffset.UtcNow)
        {
            var remaining = (failure.BlockedUntil - DateTimeOffset.UtcNow).TotalMinutes;
            _logger.LogWarning("NUC {Country} LOGIN BLOQUEADO por {Min:F0} min mas (evitar bloqueo de cuenta, {Fails} intentos fallidos)",
                config.CountryCode, remaining, failure.FailCount);
            throw new InvalidOperationException($"Login NUC {config.CountryCode} bloqueado por {remaining:F0} min para evitar bloqueo de cuenta");
        }

        try
        {
            var token = await LoginAndGetTokenAsync(config, ct);
            _loginFailures.TryRemove(config.CountryCode, out _);
            _tokenCache[config.CountryCode] = (token, DateTimeOffset.UtcNow.Add(TokenCacheDuration));
            return token;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            var current = _loginFailures.GetOrAdd(config.CountryCode, _ => (0, DateTimeOffset.MinValue));
            var newCount = current.FailCount + 1;
            var blockedUntil = newCount >= MaxLoginFailures ? DateTimeOffset.UtcNow.Add(LoginBlockDuration) : DateTimeOffset.MinValue;
            _loginFailures[config.CountryCode] = (newCount, blockedUntil);

            if (newCount >= MaxLoginFailures)
                _logger.LogError("NUC {Country} LOGIN BLOQUEADO: {Fails} intentos fallidos con 401. No se reintentara por {Min} min para evitar bloqueo de cuenta",
                    config.CountryCode, newCount, LoginBlockDuration.TotalMinutes);
            else
                _logger.LogWarning("NUC {Country} LOGIN fallido ({Fails}/{Max}): 401 Unauthorized",
                    config.CountryCode, newCount, MaxLoginFailures);

            throw;
        }
    }
    // END-FIX::BE-660::2026-04-08::AHL::Circuit breaker de login NUC para evitar bloqueo de cuenta por intentos fallidos

    private async Task<string> LoginAndGetTokenAsync(CountryConfig config, CancellationToken ct)
    {
        var username = BuildNucUsername(config);
        var password = _configuration[$"Secrets:{config.CountryCode}:NucCredentialPassword"] ?? "placeholder";
        var client = _httpClientFactory.CreateClient("NucClient");

        var loginPayload = JsonSerializer.Serialize(new { Username = username, Password = password });
        var loginContent = new StringContent(loginPayload, Encoding.UTF8, "application/json");

        _logger.LogDebug("NUC {Country} LOGIN request: {Url} user={Username}", config.CountryCode, config.NucLoginEndpoint, username);

        var loginResponse = await client.PostAsync(config.NucLoginEndpoint, loginContent, ct);
        loginResponse.EnsureSuccessStatusCode();

        var loginBody = await loginResponse.Content.ReadAsStringAsync(ct);

        _logger.LogDebug("NUC {Country} LOGIN response: {Body}", config.CountryCode, loginBody[..Math.Min(200, loginBody.Length)]);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(loginBody);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"Respuesta no-JSON en login NUC {config.CountryCode} (HTTP {loginResponse.StatusCode}): {loginBody[..Math.Min(300, loginBody.Length)]}");
        }
        using (doc)
        {
            // El API de Digifact retorna "Token" con T mayuscula
            if (doc.RootElement.TryGetProperty("Token", out var tokenProp))
                return tokenProp.GetString() ?? throw new InvalidOperationException("Token vacio en respuesta de login NUC");
            if (doc.RootElement.TryGetProperty("token", out var tokenLower))
                return tokenLower.GetString() ?? throw new InvalidOperationException("Token vacio en respuesta de login NUC");

            throw new InvalidOperationException($"Token no encontrado en respuesta de login NUC: {loginBody[..Math.Min(200, loginBody.Length)]}");
        }
    }

    internal static string BuildNucUsername(CountryConfig config)
    {
        return (config.NucUsernameFormat ?? "{Country}.{TaxId}.{NucUsername}")
            .Replace("{Country}", config.CountryCode)
            .Replace("{TaxId}", config.TaxId)
            .Replace("{NucUsername}", config.NucUsername)
            .Replace("{NRC}", config.TaxId)
            .Replace("{NIT}", config.TaxId);
    }

    private async Task<NucResponse> CertifyWithTokenAsync(
        string endpoint, string token, string xml, CountryConfig config, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("NucClient");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token);

        // Digifact NUC requiere query params FORMAT, TAXID, USERNAME
        var url = $"{endpoint}?&FORMAT=XML&TAXID={config.TaxId}&USERNAME={config.NucUsername}";
        var content = new StringContent(xml, Encoding.UTF8, "application/xml");

        _logger.LogDebug("NUC {Country} CERT request: {Url}\n{Xml}", config.CountryCode, url, xml[..Math.Min(500, xml.Length)]);

        // BEGIN-FIX::BE-672::2026-05-13::AHL::Medir SOLO la transaccion HTTP (PostAsync + ReadResponse), igual al monitoreo viejo. Excluye creacion de HttpClient, parsing JSON, etc.
        var sw = Stopwatch.StartNew();
        var response = await client.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        sw.Stop();
        var elapsedMs = sw.ElapsedMilliseconds;
        // END-FIX::BE-672::2026-05-13::AHL

        _logger.LogDebug("NUC {Country} CERT response ({StatusCode}):\n{Body}",
            config.CountryCode, response.StatusCode, body[..Math.Min(500, body.Length)]);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return new NucResponse(
                Success: false,
                ErrorMessage: $"Respuesta no-JSON del servicio NUC (HTTP {response.StatusCode}): {body[..Math.Min(300, body.Length)]}",
                ElapsedMs: elapsedMs);
        }

        using (doc)
        {
            var root = doc.RootElement;

            var code = "-1";
            if (root.TryGetProperty("code", out var c))
            {
                code = c.ValueKind == System.Text.Json.JsonValueKind.Number
                    ? c.GetInt32().ToString()
                    : c.GetString() ?? "-1";
            }
            var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;

            string? description = null;
            if (root.TryGetProperty("description", out var d))
            {
                description = d.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? string.Join(" ", d.EnumerateArray().Select(e => e.GetString()))
                    : d.GetString();
            }

            var isSuccess = code == "1";
            return new NucResponse(
                Success: isSuccess,
                ErrorMessage: isSuccess ? null : $"Code={code}, Message={message}, Desc={description?[..Math.Min(200, description?.Length ?? 0)]}",
                ElapsedMs: elapsedMs);
        }
    }

    // BEGIN-REFACTOR::BE-660::2026-04-09::AHL::Optimizar InjectNucDynamicFields con Regex en vez de XDocument para reducir latencia
    private static readonly System.Text.RegularExpressions.Regex _issuedDateTimeRegex = new(@"<IssuedDateTime>[^<]+</IssuedDateTime>", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex _guidRegex = new(@"<GUID>[^<]+</GUID>", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex _consecutivoAttrRegex = new(@"(Name=""(?:Consecutivo|Secuencia)""\s+Value="")[^""]+("")", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex _secuencialAttrRegex = new(@"(Name=""Secuencial""\s+Value="")[^""]+("")", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex _numeroDFAttrRegex = new(@"(Name=""NumeroDF""\s+Value="")[^""]+("")", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex _codigoSeguridadAttrRegex = new(@"(Name=""CodigoSeguridad""\s+Value="")[^""]+("")", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string InjectNucDynamicFields(string xml, CountryConfig config, long consecutivo)
    {
        var gtNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Guatemala"));
        var dateStr = gtNow.ToString("yyyy-MM-ddTHH:mm:ss-06:00");

        // Fecha
        xml = _issuedDateTimeRegex.Replace(xml, $"<IssuedDateTime>{dateStr}</IssuedDateTime>");

        // GUID dinámico (SV)
        if (xml.Contains("<GUID>"))
            xml = _guidRegex.Replace(xml, $"<GUID>{Guid.NewGuid().ToString().ToUpper()}</GUID>");

        // Consecutivo/Secuencia (10 dígitos)
        xml = _consecutivoAttrRegex.Replace(xml, $"${{1}}{(9900000 + consecutivo).ToString("D10")}${{2}}");

        // Secuencial SV (15 dígitos)
        xml = _secuencialAttrRegex.Replace(xml, $"${{1}}{(400000000000 + consecutivo).ToString("D15")}${{2}}");

        // NumeroDF y CodigoSeguridad solo PA
        if (config.CountryCode == "PA")
        {
            xml = _numeroDFAttrRegex.Replace(xml, $"${{1}}{(1140000000 + consecutivo)}${{2}}");
            xml = _codigoSeguridadAttrRegex.Replace(xml, $"${{1}}{(800000 + consecutivo).ToString("D9")}${{2}}");
        }

        return xml;
    }
    // END-REFACTOR::BE-660::2026-04-09::AHL::Optimizar InjectNucDynamicFields con Regex en vez de XDocument para reducir latencia

    private record NucResponse(bool Success, string? ErrorMessage, long ElapsedMs = 0);
}
// END-FEAT::BE-663::2026-03-17::AHL::Servicio de certificación NUC REST con soporte dual de autenticación (dynamic/static) y consecutivo atómico
