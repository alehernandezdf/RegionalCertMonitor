// BEGIN-FEAT::BE-662::2026-03-17::AHL::Servicio de generación QR (ADDQR) para PA
using System.Xml.Linq;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public class QrGenerationService : IQrGenerationService
{
    private readonly ILogger<QrGenerationService> _logger;

    public QrGenerationService(ILogger<QrGenerationService> logger)
    {
        _logger = logger;
    }

    public Task<string> AddQrToXmlAsync(string xmlContent, CountryConfig config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var doc = XDocument.Parse(xmlContent);
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        var qrNode = doc.Descendants(ns + "ADDQR").FirstOrDefault();
        if (qrNode != null)
        {
            qrNode.Value = config.QrCode ?? string.Empty;
        }
        else
        {
            doc.Root?.Add(new XElement(ns + "ADDQR", config.QrCode ?? string.Empty));
        }

        _logger.LogDebug("QR inyectado en XML para {Country}", config.CountryCode);
        return Task.FromResult(doc.ToString());
    }
}
// END-FEAT::BE-662::2026-03-17::AHL::Servicio de generación QR (ADDQR) para PA
