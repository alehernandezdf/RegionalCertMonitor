// BEGIN-FEAT::BE-661::2026-03-17::AHL::Orquestador de ciclo de monitoreo: certificación, persistencia, gate y notificaciones
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Certification;
using Monitoreo.Worker.Services.Notification;
using Monitoreo.Worker.Services.Persistence;
using Monitoreo.Worker.Services.Observability;

namespace Monitoreo.Worker.Services.Orchestration;

public class MonitoringOrchestrator : IMonitoringOrchestrator
{
    private readonly IEnumerable<ICertificationService> _certServices;
    private readonly IMonitoringRepository _repository;
    private readonly IEnumerable<INotificationService> _notifiers;
    private readonly INotificationGateService _notificationGate;
    private readonly IMetricsPublisher _metricsPublisher;
    private readonly ILogger<MonitoringOrchestrator> _logger;

    public MonitoringOrchestrator(
        IEnumerable<ICertificationService> certServices,
        IMonitoringRepository repository,
        IEnumerable<INotificationService> notifiers,
        INotificationGateService notificationGate,
        IMetricsPublisher metricsPublisher,
        ILogger<MonitoringOrchestrator> logger)
    {
        _certServices = certServices;
        _repository = repository;
        _notifiers = notifiers;
        _notificationGate = notificationGate;
        _metricsPublisher = metricsPublisher;
        _logger = logger;
    }

    public async Task ExecuteCycleAsync(CountryConfig config, CancellationToken ct)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["Country"] = config.CountryCode,
            ["CorrelationId"] = Guid.NewGuid().ToString("N")[..8]
        });

        _logger.LogInformation("Iniciando ciclo de monitoreo para {Country}", config.CountryCode);

        // 1. Ejecutar certificaciones habilitadas para este pais
        var results = new List<MonitoringResult>();
        foreach (var certService in _certServices)
        {
            // Filtrar tipos no habilitados para este pais
            if (certService.Type == CertificationType.API && !config.ApiEnabled)
                continue;
            if (certService.Type == CertificationType.NUC && string.IsNullOrEmpty(config.NucCertEndpoint))
                continue;
            if (certService.Type == CertificationType.ASMX && string.IsNullOrEmpty(config.AsmxEndpoint))
                continue;

            try
            {
                using var scope = _logger.BeginScope(new Dictionary<string, object>
                {
                    ["CertificationType"] = certService.Type.ToString()
                });

                var result = await certService.CertifyAsync(config, ct);
                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en certificacion {CertType} para {Country}",
                    certService.Type, config.CountryCode);
            }
        }

        // 2. Persistir resultados (SIEMPRE, independiente de flags de notificación)
        foreach (var result in results)
        {
            try
            {
                await _repository.WriteResultAsync(result, ct);
                // FEAT::BE-670::2026-03-17::AHL::Publicar métricas CloudWatch después de persistir
                await _metricsPublisher.PublishCertificationMetricAsync(result, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persistiendo resultado {Country}/{CertType}",
                    result.Country, result.CertificationType);
            }
        }

        // 3. Evaluar notificaciones para resultados con fallo o tiempo > umbral
        var alertResults = results.Where(r =>
            !r.ResultStatus || r.TransactionTimeMs > config.AlertThresholdMs).ToList();

        foreach (var result in alertResults)
        {
            await TryNotifyAsync(config, result, NotificationChannel.Email, ct);
            await TryNotifyAsync(config, result, NotificationChannel.WhatsApp, ct);
        }

        _logger.LogInformation(
            "Ciclo completado para {Country}: {Total} certificaciones, {Alerts} alertas",
            config.CountryCode, results.Count, alertResults.Count);
    }

    private async Task TryNotifyAsync(
        CountryConfig config, MonitoringResult result, NotificationChannel channel, CancellationToken ct)
    {
        var gate = await _notificationGate.EvaluateAsync(
            config.CountryCode, result.CertificationType.ToString(), channel, ct);

        if (!gate.IsAllowed)
        {
            _logger.LogDebug("Notificación {Channel} suprimida para {Country}/{CertType}: {Reason}",
                channel, config.CountryCode, result.CertificationType, gate.SuppressedReason);
            return;
        }

        var (notificationType, recipients) = channel switch
        {
            NotificationChannel.Email => (NotificationType.Email, config.EmailRecipients),
            NotificationChannel.WhatsApp => (NotificationType.WhatsApp, config.WhatsAppNumbers),
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };

        var payload = new NotificationPayload(result, notificationType, recipients);

        foreach (var notifier in _notifiers)
        {
            try
            {
                await notifier.NotifyAsync(payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando notificación {Channel} para {Country}/{CertType}",
                    channel, config.CountryCode, result.CertificationType);
            }
        }
    }
}
// END-FEAT::BE-661::2026-03-17::AHL::Orquestador de ciclo de monitoreo: certificación, persistencia, gate y notificaciones
