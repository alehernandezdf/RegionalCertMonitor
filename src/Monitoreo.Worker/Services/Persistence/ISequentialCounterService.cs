// BEGIN-FEAT::BE-660::2026-03-26::AHL::Interfaz de servicio de consecutivos persistidos por pais y tipo
namespace Monitoreo.Worker.Services.Persistence;

public interface ISequentialCounterService
{
    Task<long> GetNextAsync(string country, string certType, CancellationToken ct);
}
// END-FEAT::BE-660::2026-03-26::AHL::Interfaz de servicio de consecutivos persistidos por pais y tipo
