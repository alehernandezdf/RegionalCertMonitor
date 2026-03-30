// BEGIN-FEAT::BE-671::2026-03-17::AHL::Health check con verificación de conectividad PostgreSQL
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Monitoreo.Worker.Health;

public class MonitoringHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MonitoringHealthCheck> _logger;

    public MonitoringHealthCheck(
        IConfiguration configuration,
        ILogger<MonitoringHealthCheck> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("PostgreSQL");
            if (string.IsNullOrWhiteSpace(connectionString))
                return HealthCheckResult.Degraded("ConnectionString PostgreSQL no configurado");

            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var cmd = dataSource.CreateCommand("SELECT 1");
            await cmd.ExecuteScalarAsync(ct);

            return HealthCheckResult.Healthy("PostgreSQL conectado");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check falló");
            return HealthCheckResult.Unhealthy("PostgreSQL no disponible", ex);
        }
    }
}
// END-FEAT::BE-671::2026-03-17::AHL::Health check con verificación de conectividad PostgreSQL
