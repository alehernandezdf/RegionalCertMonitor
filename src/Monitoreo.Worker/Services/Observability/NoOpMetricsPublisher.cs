// FEAT::BE-670::2026-03-25::AHL::No-op metrics publisher para desarrollo local sin AWS CloudWatch
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Observability;

public class NoOpMetricsPublisher : IMetricsPublisher
{
    private readonly ILogger<NoOpMetricsPublisher> _logger;

    public NoOpMetricsPublisher(ILogger<NoOpMetricsPublisher> logger) => _logger = logger;

    public Task PublishCertificationMetricAsync(MonitoringResult result, CancellationToken ct)
    {
        _logger.LogDebug("[NoOp] Métrica: {Country}/{CertType} {TimeMs}ms {Status}",
            result.Country, result.CertificationType, result.TransactionTimeMs,
            result.ResultStatus ? "OK" : "FAIL");
        return Task.CompletedTask;
    }

    public Task PublishCircuitBreakerStateAsync(string country, string certType, string state, CancellationToken ct)
    {
        _logger.LogDebug("[NoOp] CircuitBreaker: {Country}/{CertType} = {State}", country, certType, state);
        return Task.CompletedTask;
    }
}
