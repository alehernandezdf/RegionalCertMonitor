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
            content.Headers.Add("SOAPAction", "https://corec.digifact.com/schema/ws/RequestTransaction");

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
                success ? null : errorMsg, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ASMX {Country} #{Consecutivo}: Error",
                config.CountryCode, consecutivo);

            return new MonitoringResult(Guid.NewGuid(), config.CountryCode, CertificationType.ASMX,
                config.AsmxEndpoint, 0, false, ex.Message, DateTimeOffset.UtcNow);
        }
    }

    private static string BuildSoapEnvelope(CountryConfig config, string xmlData)
    {
        var username = $"{config.CountryCode}.{config.TaxId}.{config.NucUsername}";
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ws=""https://corec.digifact.com/schema/ws"">
  <soap:Body>
    <ws:RequestTransaction>
      <ws:Requestor>{config.Requestor}</ws:Requestor>
      <ws:Transaction>CERTIFICATE_FE</ws:Transaction>
      <ws:Country>{config.CountryCode}</ws:Country>
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

    private static string InjectDynamicFields(string xml, CountryConfig config, long consecutivo)
    {
        var doc = XDocument.Parse(xml);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        var now = DateTimeOffset.Now;
        // Clave CR: 50 digitos exactos
        // {codigopais:3}{dia:2}{mes:2}{anio:2}{emisor:12}{sucursal:3}{puntodeventa:5}{tipodoc:2}{correlativo:10}{situacion:1}{codigdseg:8}
        var emisor = "003123456789"; // 12 digitos
        var sucursal = "000";
        var puntoVenta = "00070"; // Caja ASMX monitoreo unificado
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

        return doc.ToString();
    }
}
// END-FEAT::BE-662::2026-03-25::AHL::Servicio ASMX SOAP con RequestTransaction envelope correcto para Digifact
