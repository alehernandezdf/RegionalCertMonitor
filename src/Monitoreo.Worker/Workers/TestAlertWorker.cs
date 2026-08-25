// BEGIN-FEAT::BE-672::2026-07-20::AHL::Disparador manual de alertas de prueba via tabla alert_test_queue
// Uso: INSERT INTO alert_test_queue (channel) VALUES ('email');   -- 'email' | 'whatsapp' | 'all'
// El worker consume la fila (la borra) y envia una alerta de PRUEBA a los destinatarios activos de la BD.
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Notification;
using Npgsql;

namespace Monitoreo.Worker.Workers;

public class TestAlertWorker : BackgroundService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly INotificationRecipientsService _recipientsService;
    private readonly IEnumerable<INotificationService> _notifiers;
    private readonly ILogger<TestAlertWorker> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    public TestAlertWorker(
        IConfiguration configuration,
        INotificationRecipientsService recipientsService,
        IEnumerable<INotificationService> notifiers,
        ILogger<TestAlertWorker> logger)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQL no configurado");
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _recipientsService = recipientsService;
        _notifiers = notifiers;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TestAlertWorker iniciado: INSERT en alert_test_queue dispara alerta de prueba (poll cada {Sec}s)", PollInterval.TotalSeconds);

        using var timer = new PeriodicTimer(PollInterval);
        while (await WaitSafeAsync(timer, stoppingToken))
        {
            try
            {
                var pending = await DequeueAsync(stoppingToken);
                if (pending is null) continue;

                var (id, channelRequest) = pending.Value;
                _logger.LogWarning("ALERTA DE PRUEBA disparada manualmente (fila {Id}, canal '{Channel}')", id, channelRequest);

                if (channelRequest is "email" or "all")
                    await SendTestAsync(NotificationChannel.Email, NotificationType.Email, stoppingToken);
                if (channelRequest is "whatsapp" or "all")
                    await SendTestAsync(NotificationChannel.WhatsApp, NotificationType.WhatsApp, stoppingToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Tabla inexistente o BD caida: no es critico, seguir intentando
                _logger.LogDebug(ex, "TestAlertWorker: no se pudo consultar alert_test_queue");
            }
        }
    }

    private static async Task<bool> WaitSafeAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }

    private async Task<(int Id, string Channel)?> DequeueAsync(CancellationToken ct)
    {
        const string sql = """
            DELETE FROM alert_test_queue
            WHERE id = (SELECT id FROM alert_test_queue ORDER BY id LIMIT 1)
            RETURNING id, channel
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return (reader.GetInt32(0), reader.GetString(1).Trim().ToLowerInvariant());
    }

    private async Task SendTestAsync(NotificationChannel channel, NotificationType type, CancellationToken ct)
    {
        var recipients = await _recipientsService.GetRecipientsAsync("TEST", channel, ct);
        if (recipients.Count == 0)
        {
            _logger.LogWarning("ALERTA DE PRUEBA {Channel}: sin destinatarios activos en notification_recipients, no se envia nada", channel);
            return;
        }

        var result = new MonitoringResult(
            Id: Guid.NewGuid(),
            Country: "TEST",
            CertificationType: CertificationType.NUC,
            Endpoint: "PRUEBA MANUAL (alert_test_queue)",
            TransactionTimeMs: 0,
            ResultStatus: false,
            EventErrorMessage: "PRUEBA MANUAL del sistema de alertas. Si recibiste este mensaje, la lectura de destinatarios desde la base de datos y el envio funcionan correctamente. No hay ningun incidente.",
            CreatedAt: DateTimeOffset.UtcNow,
            RawResponse: "Alerta de PRUEBA disparada manualmente con: INSERT INTO alert_test_queue (channel) VALUES (...). Destinatarios leidos de la tabla notification_recipients.");

        var payload = new NotificationPayload(result, type, recipients);

        foreach (var notifier in _notifiers)
        {
            try
            {
                await notifier.NotifyAsync(payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ALERTA DE PRUEBA {Channel}: fallo el envio", channel);
            }
        }

        _logger.LogWarning("ALERTA DE PRUEBA {Channel} enviada a {Count} destinatarios: {Recipients}",
            channel, recipients.Count, string.Join(", ", recipients));
    }
}
// END-FEAT::BE-672::2026-07-20::AHL::Disparador manual de alertas de prueba via tabla alert_test_queue
