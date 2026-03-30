// BEGIN-FEAT::BE-661::2026-03-25::AHL::Registrador dinamico de workers por pais habilitado
using Monitoreo.Worker.Services.Orchestration;

namespace Monitoreo.Worker.Workers;

public class CountryWorkerRegistrar : BackgroundService
{
    private readonly Services.Configuration.IConfigurationProvider _configProvider;
    private readonly IMonitoringOrchestrator _orchestrator;
    private readonly ILogger<CountryWorkerRegistrar> _logger;
    private readonly List<Task> _workerTasks = [];

    public CountryWorkerRegistrar(
        Services.Configuration.IConfigurationProvider configProvider,
        IMonitoringOrchestrator orchestrator,
        ILogger<CountryWorkerRegistrar> logger)
    {
        _configProvider = configProvider;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var countries = await _configProvider.LoadAllCountriesAsync(stoppingToken);
        var enabled = countries.Where(c => c.Enabled).ToList();

        _logger.LogInformation("Paises habilitados: {Countries}",
            string.Join(", ", enabled.Select(c => c.CountryCode)));

        foreach (var country in enabled)
        {
            var cc = country.CountryCode;
            var interval = TimeSpan.FromSeconds(country.MonitoringIntervalSeconds);

            _workerTasks.Add(Task.Run(async () =>
            {
                _logger.LogInformation("Worker {Country} iniciado (intervalo: {Interval}s)",
                    cc, country.MonitoringIntervalSeconds);

                using var timer = new PeriodicTimer(interval);

                // Ejecutar inmediatamente la primera vez
                await RunCycleAsync(cc, stoppingToken);

                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await RunCycleAsync(cc, stoppingToken);
                }
            }, stoppingToken));
        }

        await Task.WhenAll(_workerTasks);
    }

    private async Task RunCycleAsync(string countryCode, CancellationToken ct)
    {
        try
        {
            var config = await _configProvider.LoadCountryAsync(countryCode, ct);
            await _orchestrator.ExecuteCycleAsync(config, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ciclo de monitoreo para {Country}", countryCode);
        }
    }
}
// END-FEAT::BE-661::2026-03-25::AHL::Registrador dinamico de workers por pais habilitado
