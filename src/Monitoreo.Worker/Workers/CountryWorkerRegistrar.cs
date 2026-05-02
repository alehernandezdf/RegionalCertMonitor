// BEGIN-FEAT::BE-661::2026-03-25::AHL::Registrador dinamico de workers por pais habilitado
using Monitoreo.Worker.Services.Orchestration;

namespace Monitoreo.Worker.Workers;

public class CountryWorkerRegistrar : BackgroundService
{
    private readonly Services.Configuration.IConfigurationProvider _configProvider;
    private readonly IMonitoringOrchestrator _orchestrator;
    private readonly ILogger<CountryWorkerRegistrar> _logger;
    private readonly List<Task> _workerTasks = [];
    // BEGIN-FIX::BE-672::2026-05-01::AHL::SemaphoreSlim por pais para evitar solapamiento de ciclos que genera ceros falsos
    private readonly Dictionary<string, SemaphoreSlim> _cycleGuards = [];
    // END-FIX::BE-672::2026-05-01::AHL::SemaphoreSlim por pais para evitar solapamiento de ciclos que genera ceros falsos

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

        // BEGIN-FIX::BE-672::2026-05-01::AHL::Crear semaforo por pais para evitar solapamiento
        foreach (var country in enabled)
            _cycleGuards[country.CountryCode] = new SemaphoreSlim(1, 1);
        // END-FIX::BE-672::2026-05-01::AHL::Crear semaforo por pais para evitar solapamiento

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
        // BEGIN-FIX::BE-672::2026-05-01::AHL::Si el ciclo anterior sigue corriendo, saltar este tick para evitar ceros falsos
        var guard = _cycleGuards[countryCode];
        if (!await guard.WaitAsync(0, ct))
        {
            _logger.LogWarning("Worker {Country}: ciclo anterior aun en ejecucion, saltando tick", countryCode);
            return;
        }
        // END-FIX::BE-672::2026-05-01::AHL::Si el ciclo anterior sigue corriendo, saltar este tick para evitar ceros falsos
        try
        {
            var config = await _configProvider.LoadCountryAsync(countryCode, ct);
            await _orchestrator.ExecuteCycleAsync(config, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en ciclo de monitoreo para {Country}", countryCode);
        }
        finally
        {
            guard.Release();
        }
    }
}
// END-FEAT::BE-661::2026-03-25::AHL::Registrador dinamico de workers por pais habilitado
