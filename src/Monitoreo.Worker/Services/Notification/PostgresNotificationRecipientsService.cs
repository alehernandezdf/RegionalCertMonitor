// BEGIN-FEAT::BE-672::2026-07-20::AHL::Destinatarios de notificaciones desde PostgreSQL: agregar/quitar correos y numeros sin deploy (INSERT/UPDATE en notification_recipients)
using Monitoreo.Worker.Models;
using Npgsql;

namespace Monitoreo.Worker.Services.Notification;

public class PostgresNotificationRecipientsService : INotificationRecipientsService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresNotificationRecipientsService> _logger;

    public PostgresNotificationRecipientsService(
        IConfiguration configuration,
        ILogger<PostgresNotificationRecipientsService> logger)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQL no configurado");
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetRecipientsAsync(
        string countryCode, NotificationChannel channel, CancellationToken ct)
    {
        const string sql = """
            SELECT destination
            FROM notification_recipients
            WHERE enabled = true
              AND channel = @channel
              AND (country = @country OR country = '*')
            ORDER BY destination
            """;

        try
        {
            await using var cmd = _dataSource.CreateCommand(sql);
            cmd.Parameters.AddWithValue("channel", channel == NotificationChannel.Email ? "email" : "whatsapp");
            cmd.Parameters.AddWithValue("country", countryCode);

            var recipients = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                recipients.Add(reader.GetString(0));

            return recipients.AsReadOnly();
        }
        catch (Exception ex)
        {
            // Tabla inexistente o BD caida: devolver vacio para que el orquestador use el fallback de appsettings
            _logger.LogWarning(ex, "No se pudieron leer destinatarios de BD para {Country}/{Channel}, se usara fallback de configuracion",
                countryCode, channel);
            return Array.Empty<string>();
        }
    }
}
// END-FEAT::BE-672::2026-07-20::AHL::Destinatarios de notificaciones desde PostgreSQL
