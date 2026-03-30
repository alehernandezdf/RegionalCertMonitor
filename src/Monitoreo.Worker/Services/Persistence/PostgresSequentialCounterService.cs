// BEGIN-FEAT::BE-660::2026-03-26::AHL::Servicio de consecutivos persistidos en PostgreSQL con incremento atomico
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace Monitoreo.Worker.Services.Persistence;

public class PostgresSequentialCounterService : ISequentialCounterService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresSequentialCounterService> _logger;

    public PostgresSequentialCounterService(IConfiguration configuration, ILogger<PostgresSequentialCounterService> logger)
    {
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQL no configurado");
        _dataSource = NpgsqlDataSource.Create(connectionString);
        _logger = logger;
    }

    public async Task<long> GetNextAsync(string country, string certType, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO sequential_counters (country, cert_type, last_value, updated_at)
            VALUES (@country, @certType, 1, NOW())
            ON CONFLICT (country, cert_type)
            DO UPDATE SET last_value = sequential_counters.last_value + 1, updated_at = NOW()
            RETURNING last_value;";

        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddWithValue("country", country);
        cmd.Parameters.AddWithValue("certType", certType);

        var result = await cmd.ExecuteScalarAsync(ct);
        var value = Convert.ToInt64(result);

        _logger.LogDebug("Consecutivo {Country}/{CertType}: {Value}", country, certType, value);
        return value;
    }
}
// END-FEAT::BE-660::2026-03-26::AHL::Servicio de consecutivos persistidos en PostgreSQL con incremento atomico
