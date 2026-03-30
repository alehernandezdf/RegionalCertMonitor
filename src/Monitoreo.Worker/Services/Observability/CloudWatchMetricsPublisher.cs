// BEGIN-FEAT::BE-670::2026-03-17::AHL::Publicación de métricas custom en CloudWatch namespace Digifact/Monitoreo
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Observability;

public class CloudWatchMetricsPublisher : IMetricsPublisher
{
    private readonly IAmazonCloudWatch _cloudWatch;
    private readonly ILogger<CloudWatchMetricsPublisher> _logger;
    private const string Namespace = "Digifact/Monitoreo";

    public CloudWatchMetricsPublisher(
        IAmazonCloudWatch cloudWatch,
        ILogger<CloudWatchMetricsPublisher> logger)
    {
        _cloudWatch = cloudWatch;
        _logger = logger;
    }

    public async Task PublishCertificationMetricAsync(MonitoringResult result, CancellationToken ct)
    {
        var dimensions = new List<Dimension>
        {
            new() { Name = "Country", Value = result.Country },
            new() { Name = "CertificationType", Value = result.CertificationType.ToString() }
        };

        var metricData = new List<MetricDatum>
        {
            new()
            {
                MetricName = "transaction_time_ms",
                Value = result.TransactionTimeMs,
                Unit = StandardUnit.Milliseconds,
                Dimensions = dimensions,
                Timestamp = DateTime.UtcNow
            },
            new()
            {
                MetricName = result.ResultStatus ? "success_count" : "failure_count",
                Value = 1,
                Unit = StandardUnit.Count,
                Dimensions = dimensions,
                Timestamp = DateTime.UtcNow
            }
        };

        try
        {
            await _cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
            {
                Namespace = Namespace,
                MetricData = metricData
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error publicando métricas CloudWatch para {Country}/{CertType}",
                result.Country, result.CertificationType);
        }
    }

    public async Task PublishCircuitBreakerStateAsync(
        string country, string certType, string state, CancellationToken ct)
    {
        var datum = new MetricDatum
        {
            MetricName = "circuit_breaker_state",
            Value = state == "Open" ? 1 : 0,
            Unit = StandardUnit.None,
            Dimensions = new List<Dimension>
            {
                new() { Name = "Country", Value = country },
                new() { Name = "CertificationType", Value = certType },
                new() { Name = "State", Value = state }
            },
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await _cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
            {
                Namespace = Namespace,
                MetricData = [datum]
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error publicando estado circuit breaker para {Country}/{CertType}",
                country, certType);
        }
    }
}
// END-FEAT::BE-670::2026-03-17::AHL::Publicación de métricas custom en CloudWatch namespace Digifact/Monitoreo
