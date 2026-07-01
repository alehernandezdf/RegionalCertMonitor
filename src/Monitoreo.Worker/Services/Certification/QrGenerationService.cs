// BEGIN-FEAT::BE-662::2026-07-01::AHL::Generación QR (gNoFirm/dQRCode) para PA con JWT HS256, portado del monitoreo viejo (ADDQR + GetJWT)
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public class QrGenerationService : IQrGenerationService
{
    private readonly ILogger<QrGenerationService> _logger;
    private const string FeNamespace = "http://dgi-fep.mef.gob.pa";
    private const string DsNamespace = "http://www.w3.org/2000/09/xmldsig#";

    public QrGenerationService(ILogger<QrGenerationService> logger)
    {
        _logger = logger;
    }

    public Task<string> AddQrToXmlAsync(string xmlContent, CountryConfig config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(config.QrCode))
        {
            _logger.LogWarning("QR {Country}: QrCode (llave JWT) no configurado, omitiendo QR", config.CountryCode);
            return Task.FromResult(xmlContent);
        }

        // PreserveWhitespace: NO reformatear el documento ya firmado (invalidaría la firma)
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(xmlContent);

        var nsMgr = new XmlNamespaceManager(doc.NameTable);
        nsMgr.AddNamespace("ds", DsNamespace);
        nsMgr.AddNamespace("fe", FeNamespace);

        var digestNode = doc.SelectSingleNode("//ds:Reference/ds:DigestValue", nsMgr)
            ?? throw new InvalidOperationException("DigestValue no encontrado: el XML debe estar firmado antes de agregar el QR");
        var digestValue = digestNode.InnerText;

        var cufe = doc.SelectSingleNode("/fe:rFE/fe:dId", nsMgr)?.InnerText
            ?? throw new InvalidOperationException("dId (CUFE) no encontrado en rFE");
        var ambiente = doc.SelectSingleNode("/fe:rFE/fe:gDGen/fe:iAmb", nsMgr)?.InnerText ?? "1";

        var jwt = CreateJwtHs256(config.QrCode, cufe, ambiente, digestValue);

        var baseUrl = ambiente == "2"
            ? "https://dgi-fep-test.mef.gob.pa:40001/Consultas/FacturasPorQR"
            : "https://dgi-fep.mef.gob.pa/Consultas/FacturasPorQR";
        var qrUrl = $"{baseUrl}?chFE={cufe}&iAmb={ambiente}&digestValue={digestValue}&jwt={jwt}";

        var gNoFirm = doc.CreateElement("gNoFirm", FeNamespace);
        var dQrCode = doc.CreateElement("dQRCode", FeNamespace);
        dQrCode.AppendChild(doc.CreateCDataSection(qrUrl));
        gNoFirm.AppendChild(dQrCode);
        doc.DocumentElement!.AppendChild(gNoFirm);

        _logger.LogDebug("QR agregado para {Country}: {UrlPrefix}...", config.CountryCode, qrUrl[..Math.Min(80, qrUrl.Length)]);
        return Task.FromResult(doc.OuterXml);
    }

    // JWT HS256 con claims {chFE, iAmb, digestValue}, igual al GetJWT del monitoreo viejo (Chilkat)
    private static string CreateJwtHs256(string key, string cufe, string ambiente, string digestValue)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { chFE = cufe, iAmb = ambiente, digestValue }));
        var signingInput = $"{header}.{payload}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var signature = Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput)));

        return $"{signingInput}.{signature}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
// END-FEAT::BE-662::2026-07-01::AHL::Generación QR (gNoFirm/dQRCode) para PA con JWT HS256, portado del monitoreo viejo
