// BEGIN-FEAT::BE-661::2026-03-17::AHL::BackgroundService por país con PeriodicTimer, manejo de errores sin detener ciclo
using Monitoreo.Worker.Services.Configuration;
using Monitoreo.Worker.Services.Orchestration;

namespace Monitoreo.Worker.Workers;

public class CountryMonitoringWorker : BackgroundService
{
    private readonly string _countryCode;
    private readonly Services.Configuration.IConfigurationProvider _configProvider;
    private readonly IMonitoringOrchestrator _orchestrator;
    private readonly ILogger<CountryMonitoringWorker> _logger;

    public CountryMonitoringWorker(
        string countryCode,
        Services.Configuration.IConfigurationProvider configProvider,
        IMonitoringOrchestrator orchestrator,
        ILogger<CountryMonitoringWorker> logger)
    {
        _countryCode = countryCode;
        _configProvider = configProvider;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker iniciado para {Country}", _countryCode);

        var config = await _configProvider.LoadCountryAsync(_countryCode, stoppingToken);
        var interval = TimeSpan.FromSeconds(config.MonitoringIntervalSeconds);

        using var timer = new PeriodicTimer(interval);

        // Ejecutar inmediatamente al iniciar
        await RunCycleAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCycleAsync(stoppingToken);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            var config = await _configProvider.LoadCountryAsync(_countryCode, ct);
            await _orchestrator.ExecuteCycleAsync(config, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Worker {Country} detenido por cancelación", _countryCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ciclo de monitoreo para {Country}. Esperando siguiente tick",
                _countryCode);
        }
    }
}
// END-FEAT::BE-661::2026-03-17::AHL::BackgroundService por país con PeriodicTimer, manejo de errores sin detener ciclo
