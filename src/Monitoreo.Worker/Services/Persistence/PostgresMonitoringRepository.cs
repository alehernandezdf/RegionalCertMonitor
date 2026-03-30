// BEGIN-FEAT::BE-664::2026-03-17::AHL::Repositorio PostgreSQL con Npgsql, connection pooling y parámetros tipados
using Monitoreo.Worker.Models;
using Npgsql;

namespace Monitoreo.Worker.Services.Persistence;

public class PostgresMonitoringRepository : IMonitoringRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresMonitoringRepository> _logger;

    public PostgresMonitoringRepository(
        IConfiguration configuration,
        ILogger<PostgresMonitoringRepository> logger)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQL no configurado");

        _dataSource = NpgsqlDataSource.Create(connectionString);
        _logger = logger;
    }

    public async Task WriteResultAsync(MonitoringResult result, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO monitoring_results
                (id, country, certification_type, endpoint, transaction_time_ms, result_status, event_error_message, created_at)
            VALUES
                (@id, @country, @certType, @endpoint, @timeMs, @status, @error, @createdAt)
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", result.Id);
        cmd.Parameters.AddWithValue("country", result.Country);
        cmd.Parameters.AddWithValue("certType", result.CertificationType.ToString());
        cmd.Parameters.AddWithValue("endpoint", result.Endpoint);
        cmd.Parameters.AddWithValue("timeMs", result.TransactionTimeMs);
        cmd.Parameters.AddWithValue("status", result.ResultStatus);
        cmd.Parameters.AddWithValue("error", (object?)result.EventErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("createdAt", result.CreatedAt);

        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogDebug("Resultado persistido: {Country}/{CertType} en {TimeMs}ms",
            result.Country, result.CertificationType, result.TransactionTimeMs);
    }

    public async Task<IReadOnlyList<MonitoringResult>> GetRecentResultsAsync(string country, int limit, CancellationToken ct)
    {
        const string sql = """
            SELECT id, country, certification_type, endpoint, transaction_time_ms,
                   result_status, event_error_message, created_at
            FROM monitoring_results
            WHERE country = @country
            ORDER BY created_at DESC
            LIMIT @limit
            """;

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("country", country);
        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var results = new List<MonitoringResult>();

        while (await reader.ReadAsync(ct))
        {
            results.Add(new MonitoringResult(
                Id: reader.GetGuid(0),
                Country: reader.GetString(1),
                CertificationType: Enum.Parse<CertificationType>(reader.GetString(2)),
                Endpoint: reader.GetString(3),
                TransactionTimeMs: reader.GetInt64(4),
                ResultStatus: reader.GetBoolean(5),
                EventErrorMessage: reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt: reader.GetFieldValue<DateTimeOffset>(7)));
        }

        return results.AsReadOnly();
    }
}
// END-FEAT::BE-664::2026-03-17::AHL::Repositorio PostgreSQL con Npgsql, connection pooling y parámetros tipados
