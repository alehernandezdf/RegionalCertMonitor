// BEGIN-FEAT::BE-662::2026-03-25::AHL::Servicio ASMX SOAP con RequestTransaction envelope correcto para Digifact
using System.Diagnostics;
using System.Xml.Linq;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Persistence;

namespace Monitoreo.Worker.Services.Certification;

public class AsmxCertificationService : ICertificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAsmxPreProcessingPipeline _pipeline;
    private readonly ISequentialCounterService _counterService;
    private readonly ILogger<AsmxCertificationService> _logger;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _templateCache = new();

    public CertificationType Type => CertificationType.ASMX;

    public AsmxCertificationService(
        IHttpClientFactory httpClientFactory,
        IAsmxPreProcessingPipeline pipeline,
        ISequentialCounterService counterService,
        ILogger<AsmxCertificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _pipeline = pipeline;
        _counterService = counterService;
        _logger = logger;
    }

    public async Task<MonitoringResult> CertifyAsync(CountryConfig config, CancellationToken ct)
    {
        var consecutivo = await _counterService.GetNextAsync(config.CountryCode, "ASMX", ct);

        try
        {
            var templateXml = _templateCache.GetOrAdd(config.AsmxTemplatePath, path => File.ReadAllText(path));
            var xml = InjectDynamicFields(templateXml, config, consecutivo);
            xml = await _pipeline.ProcessAsync(xml, config, ct);

            var soapEnvelope = BuildSoapEnvelope(config, xml);
            var client = _httpClientFactory.CreateClient("AsmxClient");
            var content = new StringContent(soapEnvelope, System.Text.Encoding.UTF8, "text/xml");

            var soapAction = GetWsNamespace(config.CountryCode) + "/RequestTransaction";
            content.Headers.Add("SOAPAction", soapAction);

            _logger.LogDebug("ASMX {Country} #{Consecutivo} REQUEST enviando...", config.CountryCode, consecutivo);

            // Medir SOLO la llamada HTTP, igual que el servicio viejo
            var sw = Stopwatch.StartNew();
            var response = await client.PostAsync(config.AsmxEndpoint, content, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            sw.Stop();

            _logger.LogDebug("ASMX {Country} #{Consecutivo} RESPONSE ({StatusCode}):\n{Body}",
                config.CountryCode, consecutivo, response.StatusCode, responseBody);

            var (success, errorMsg) = ParseSoapResponse(responseBody);

            _logger.LogInformation("ASMX {Country} #{Consecutivo}: {Status} en {TimeMs}ms",
                config.CountryCode, consecutivo, success ? "OK" : "FAIL", sw.ElapsedMilliseconds);

            return new MonitoringResult(Guid.NewGuid(), config.CountryCode, CertificationType.ASMX,
                config.AsmxEndpoint, sw.ElapsedMilliseconds, success,
                success ? null : errorMsg, DateTimeOffset.UtcNow, RawResponse: responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ASMX {Country} #{Consecutivo}: Error",
                config.CountryCode, consecutivo);

            return new MonitoringResult(Guid.NewGuid(), config.CountryCode, CertificationType.ASMX,
                config.AsmxEndpoint, 0, false, ex.Message, DateTimeOffset.UtcNow, RawResponse: ex.ToString());
        }
    }

    // BEGIN-FEAT::BE-662::2026-07-01::AHL::Namespace del WS por país (PA tiene el suyo propio)
    private static string GetWsNamespace(string countryCode) => countryCode switch
    {
        var c when c.StartsWith("GT") => "http://www.fact.com.mx/schema/ws",
        "PA" => "https://www.digifact.com.pa/schema/ws",
        _ => "https://corec.digifact.com/schema/ws"
    };
    // END-FEAT::BE-662::2026-07-01::AHL::Namespace del WS por país

    private static string BuildSoapEnvelope(CountryConfig config, string xmlData)
    {
        var username = config.AsmxUsernameFormat != null
            ? config.AsmxUsernameFormat
                .Replace("{Country}", config.CountryCode)
                .Replace("{TaxId}", config.TaxId)
                .Replace("{NucUsername}", config.NucUsername)
            : $"{config.CountryCode}.{config.TaxId}.{config.NucUsername}";

        var actualCountry = config.CountryCode.StartsWith("GT") ? "GT" : config.CountryCode;
        var transaction = config.AsmxTransactionType;
        var wsNamespace = GetWsNamespace(config.CountryCode);

        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ws=""{wsNamespace}"">
  <soap:Body>
    <ws:RequestTransaction>
      <ws:Requestor>{config.Requestor}</ws:Requestor>
      <ws:Transaction>{transaction}</ws:Transaction>
      <ws:Country>{actualCountry}</ws:Country>
      <ws:Entity>{config.TaxId}</ws:Entity>
      <ws:User>{config.Requestor}</ws:User>
      <ws:UserName>{username}</ws:UserName>
      <ws:Data1>{System.Security.SecurityElement.Escape(xmlData)}</ws:Data1>
      <ws:Data2></ws:Data2>
      <ws:Data3>XML</ws:Data3>
    </ws:RequestTransaction>
  </soap:Body>
</soap:Envelope>";
    }

    private static (bool success, string? error) ParseSoapResponse(string responseBody)
    {
        try
        {
            var doc = XDocument.Parse(responseBody);
            var fault = doc.Descendants("faultstring").FirstOrDefault();
            if (fault != null) return (false, fault.Value);

            var resultNode = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Result");
            if (resultNode != null)
            {
                var isSuccess = string.Equals(resultNode.Value, "true", StringComparison.OrdinalIgnoreCase);
                if (!isSuccess)
                {
                    var dataNode = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Data");
                    return (false, dataNode?.Value ?? "Certificacion fallida");
                }
                return (true, null);
            }
            return (false, responseBody[..Math.Min(500, responseBody.Length)]);
        }
        catch { return (false, responseBody[..Math.Min(500, responseBody.Length)]); }
    }

    // BEGIN-FIX::BE-676::2026-03-31::AHL::InjectDynamicFields con soporte SV (GUID, Secuencial 15 dígitos)
    private static string InjectDynamicFields(string xml, CountryConfig config, long consecutivo)
    {
        var doc = XDocument.Parse(xml);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Guatemala"));

        if (config.CountryCode.StartsWith("GT"))
        {
            // GT: solo actualizar FechaHoraEmision en namespace dte:
            XNamespace dte = "http://www.sat.gob.gt/dte/fel/0.2.0";
            var datosGenerales = doc.Descendants(dte + "DatosGenerales").FirstOrDefault();
            if (datosGenerales != null)
            {
                datosGenerales.SetAttributeValue("FechaHoraEmision", now.ToString("yyyy-MM-ddTHH:mm:ss"));
            }
        }
        else if (config.CountryCode == "SV")
        {
            // SV: GUID dinámico + IssuedDateTime + Secuencial 15 dígitos
            var guidNode = doc.Descendants("GUID").FirstOrDefault();
            if (guidNode != null)
                guidNode.Value = Guid.NewGuid().ToString().ToUpper();

            var issued = doc.Descendants("IssuedDateTime").FirstOrDefault();
            if (issued != null)
                issued.Value = now.ToString("yyyy-MM-ddTHH:mm:ss-06:00");

            foreach (var info in doc.Descendants("Info").ToList())
            {
                var name = info.Attribute("Name")?.Value;
                if (name == "Secuencial")
                    info.SetAttributeValue("Value", (6000000000 + consecutivo).ToString("D15"));
            }
        }
        else if (config.CountryCode == "PA")
        {
            // BEGIN-FEAT::BE-662::2026-07-01::AHL::Campos dinámicos ASMX PA: dNroDF/dSeg con contador atómico (base distinta a NUC para no colisionar) + dFechaEm
            XNamespace fe = "http://dgi-fep.mef.gob.pa";
            var paNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Panama"));
            var gDGen = doc.Root?.Element(fe + "gDGen");
            if (gDGen != null)
            {
                // Base 2140000000 (NUC usa 1140000000) para que nunca se crucen los rangos
                gDGen.Element(fe + "dNroDF")?.SetValue((2140000000L + consecutivo).ToString());
                gDGen.Element(fe + "dSeg")?.SetValue((700000000 + consecutivo).ToString("D9"));
                gDGen.Element(fe + "dFechaEm")?.SetValue(paNow.ToString("yyyy-MM-ddTHH:mm:ss-05:00"));
            }
            // END-FEAT::BE-662::2026-07-01::AHL::Campos dinámicos ASMX PA
        }
        else
        {
            // CR y otros: actualizar Clave, FechaEmision, NumeroConsecutivo
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var emisor = "003123456789";
            var sucursal = "000";
            var puntoVenta = "00070";
            var tipodoc = "01";
            var correlativo = consecutivo.ToString("D10");
            var situacion = "1";
            var codigoSeg = "00000001";

            var clave = $"506{now:ddMMyy}{emisor}{sucursal}{puntoVenta}{tipodoc}{correlativo}{situacion}{codigoSeg}";
            var numConsecutivo = $"{sucursal}{puntoVenta}{tipodoc}{correlativo}";

            var claveNode = doc.Descendants(ns + "Clave").FirstOrDefault();
            if (claveNode != null) claveNode.Value = clave;

            var fecha = doc.Descendants(ns + "FechaEmision").FirstOrDefault();
            if (fecha != null) fecha.Value = now.ToString("yyyy-MM-ddTHH:mm:ss");

            var numConsec = doc.Descendants(ns + "NumeroConsecutivo").FirstOrDefault();
            if (numConsec != null) numConsec.Value = numConsecutivo;
        }

        return doc.ToString();
    }
    // END-FIX::BE-676::2026-03-31::AHL::InjectDynamicFields con soporte SV (GUID, Secuencial 15 dígitos)
}
// END-FEAT::BE-662::2026-03-25::AHL::Servicio ASMX SOAP con RequestTransaction envelope correcto para Digifact
