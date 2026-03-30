// BEGIN-FEAT::BE-670::2026-03-17::AHL::Interfaz de publicación de métricas custom CloudWatch
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Observability;

public interface IMetricsPublisher
{
    Task PublishCertificationMetricAsync(MonitoringResult result, CancellationToken ct);
    Task PublishCircuitBreakerStateAsync(string country, string certType, string state, CancellationToken ct);
}
// END-FEAT::BE-670::2026-03-17::AHL::Interfaz de publicación de métricas custom CloudWatch
