// BEGIN-FEAT::BE-662::2026-03-17::AHL::Interfaz de firma PFX para PA y DO
namespace Monitoreo.Worker.Services.Certification;

public interface IPfxSigningService
{
    Task<string> SignXmlAsync(string xmlContent, string pfxBase64, string pfxPassword, CancellationToken ct);
}
// END-FEAT::BE-662::2026-03-17::AHL::Interfaz de firma PFX para PA y DO
