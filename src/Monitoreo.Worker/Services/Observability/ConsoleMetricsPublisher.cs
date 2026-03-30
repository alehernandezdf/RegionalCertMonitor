// BEGIN-FEAT::BE-670::2026-03-25::AHL::Stub de métricas para desarrollo local sin AWS CloudWatch
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Observability;

public class ConsoleMetricsPublisher : IMetricsPublisher
{
    private readonly ILogger<ConsoleMetricsPublisher> _logger;

    public ConsoleMetricsPublisher(ILogger<ConsoleMetricsPublisher> logger) => _logger = logger;

    public Task PublishCertificationMetricAsync(MonitoringResult result, CancellationToken ct)
    {
        _logger.LogInformation("[METRIC] {Country}/{CertType}: {TimeMs}ms {Status}",
            result.Country, result.CertificationType, result.TransactionTimeMs,
            result.ResultStatus ? "SUCCESS" : "FAILURE");
        return Task.CompletedTask;
    }

    public Task PublishCircuitBreakerStateAsync(string country, string certType, string state, CancellationToken ct)
    {
        _logger.LogInformation("[METRIC] CircuitBreaker {Country}/{CertType}: {State}", country, certType, state);
        return Task.CompletedTask;
    }
}
// END-FEAT::BE-670::2026-03-25::AHL::Stub de métricas para desarrollo local sin AWS CloudWatch
