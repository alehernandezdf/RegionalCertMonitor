// BEGIN-FEAT::BE-662::2026-03-17::AHL::Interfaz de generación CUFE+JWT para PA
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public interface ICufeGenerationService
{
    Task<CufeResult> GenerateCufeAsync(string xmlContent, CountryConfig config, CancellationToken ct);
}
// END-FEAT::BE-662::2026-03-17::AHL::Interfaz de generación CUFE+JWT para PA
