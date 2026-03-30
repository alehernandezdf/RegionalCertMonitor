// BEGIN-FEAT::BE-664::2026-03-17::AHL::Interfaz de repositorio de resultados de monitoreo
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Persistence;

public interface IMonitoringRepository
{
    Task WriteResultAsync(MonitoringResult result, CancellationToken ct);
    Task<IReadOnlyList<MonitoringResult>> GetRecentResultsAsync(string country, int limit, CancellationToken ct);
}
// END-FEAT::BE-664::2026-03-17::AHL::Interfaz de repositorio de resultados de monitoreo
