// BEGIN-FEAT::BE-661::2026-03-17::AHL::Interfaz del orquestador de ciclo de monitoreo
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Orchestration;

public interface IMonitoringOrchestrator
{
    Task ExecuteCycleAsync(CountryConfig config, CancellationToken ct);
}
// END-FEAT::BE-661::2026-03-17::AHL::Interfaz del orquestador de ciclo de monitoreo
