// BEGIN-FEAT::BE-662::2026-03-17::AHL::Interfaz de servicio de certificación con CertifyAsync y tipo
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Certification;

public interface ICertificationService
{
    Task<MonitoringResult> CertifyAsync(CountryConfig config, CancellationToken ct);
    CertificationType Type { get; }
}
// END-FEAT::BE-662::2026-03-17::AHL::Interfaz de servicio de certificación con CertifyAsync y tipo
