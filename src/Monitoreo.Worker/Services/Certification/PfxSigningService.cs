// BEGIN-FEAT::BE-662::2026-03-17::AHL::Servicio de firma PFX con X509Certificate2 y SignedXml para PA y DO
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Monitoreo.Worker.Services.Certification;

public class PfxSigningService : IPfxSigningService
{
    private readonly ILogger<PfxSigningService> _logger;

    public PfxSigningService(ILogger<PfxSigningService> logger)
    {
        _logger = logger;
    }

    public Task<string> SignXmlAsync(string xmlContent, string pfxBase64, string pfxPassword, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var pfxBytes = Convert.FromBase64String(pfxBase64);
        using var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, pfxPassword, X509KeyStorageFlags.EphemeralKeySet);

        if (cert.NotAfter < DateTimeOffset.UtcNow)
            throw new InvalidOperationException($"Certificado PFX expirado: {cert.NotAfter:yyyy-MM-dd}");

        var xmlDoc = new XmlDocument { PreserveWhitespace = true };
        xmlDoc.LoadXml(xmlContent);

        var signedXml = new SignedXml(xmlDoc)
        {
            SigningKey = cert.GetRSAPrivateKey()
                ?? throw new InvalidOperationException("No se encontró clave privada RSA en el PFX")
        };

        var reference = new Reference { Uri = "" };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signedXml.AddReference(reference);

        signedXml.ComputeSignature();
        var signatureElement = signedXml.GetXml();
        xmlDoc.DocumentElement!.AppendChild(xmlDoc.ImportNode(signatureElement, true));

        _logger.LogDebug("XML firmado con PFX. Subject: {Subject}", cert.Subject);
        return Task.FromResult(xmlDoc.OuterXml);
    }
}
// END-FEAT::BE-662::2026-03-17::AHL::Servicio de firma PFX con X509Certificate2 y SignedXml para PA y DO
