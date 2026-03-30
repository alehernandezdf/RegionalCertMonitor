// BEGIN-FEAT::BE-662::2026-03-17::AHL::Interfaz de pipeline de pre-procesamiento ASMX (PFX, QR, CUFE)
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public interface IAsmxPreProcessingPipeline
{
    Task<string> ProcessAsync(string xmlContent, CountryConfig config, CancellationToken ct);
}
// END-FEAT::BE-662::2026-03-17::AHL::Interfaz de pipeline de pre-procesamiento ASMX (PFX, QR, CUFE)
