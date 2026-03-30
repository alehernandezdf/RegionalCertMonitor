// BEGIN-FEAT::BE-676::2026-03-17::AHL::BackgroundService de retención con DELETE de registros > 365 días, schedule configurable
using Npgsql;

namespace Monitoreo.Worker.Services.Retention;

public class DataRetentionService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataRetentionService> _logger;

    public DataRetentionService(
        IConfiguration configuration,
        ILogger<DataRetentionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retentionDays = _configuration.GetValue("Monitoring:DataRetention:RetentionDays", 365);
        var enabled = _configuration.GetValue("Monitoring:DataRetention:Enabled", true);

        if (!enabled)
        {
            _logger.LogInformation("Retención de datos deshabilitada");
            return;
        }

        _logger.LogInformation("Retención de datos iniciada: eliminar registros > {Days} días", retentionDays);

        // Esperar hasta las 2 AM para la primera ejecución
        await WaitUntilNextRunAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        await RunRetentionAsync(retentionDays, stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunRetentionAsync(retentionDays, stoppingToken);
        }
    }

    private async Task RunRetentionAsync(int retentionDays, CancellationToken ct)
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("PostgreSQL");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning("ConnectionString PostgreSQL no configurado, omitiendo retención");
                return;
            }

            await using var dataSource = NpgsqlDataSource.Create(connectionString);
            await using var cmd = dataSource.CreateCommand(
                "DELETE FROM monitoring_results WHERE created_at < NOW() - INTERVAL '$1 days'");
            cmd.Parameters.AddWithValue(retentionDays);

            var deleted = await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Retención: {Deleted} registros eliminados (> {Days} días)", deleted, retentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en retención de datos. No afecta monitoreo");
        }
    }

    private static async Task WaitUntilNextRunAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var next2Am = now.Date.AddHours(26); // Próximas 2 AM UTC
        if (next2Am <= now)
            next2Am = next2Am.AddDays(1);

        var delay = next2Am - now;
        await Task.Delay(delay, ct);
    }
}
// END-FEAT::BE-676::2026-03-17::AHL::BackgroundService de retención con DELETE de registros > 365 días, schedule configurable
