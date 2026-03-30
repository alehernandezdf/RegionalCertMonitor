// BEGIN-FEAT::BE-662::2026-03-17::AHL::Interfaz de generación QR para PA
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public interface IQrGenerationService
{
    Task<string> AddQrToXmlAsync(string xmlContent, CountryConfig config, CancellationToken ct);
}
// END-FEAT::BE-662::2026-03-17::AHL::Interfaz de generación QR para PA
