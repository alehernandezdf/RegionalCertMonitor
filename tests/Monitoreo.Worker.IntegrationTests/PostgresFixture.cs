// BEGIN-TEST::BE-673::2026-03-17::AHL::Fixture compartido de Testcontainers PostgreSQL para tests de integración
using Npgsql;
using Testcontainers.PostgreSql;

namespace Monitoreo.Worker.IntegrationTests;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("monitoreo_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await InitializeSchemaAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task InitializeSchemaAsync()
    {
        var initSql = await File.ReadAllTextAsync(FindInitSql());
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(initSql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string FindInitSql()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "src", "Monitoreo.Worker", "Database", "init.sql");
            if (File.Exists(candidate)) return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException("init.sql not found");
    }
}

[CollectionDefinition("Postgres")]
public class PostgresCollection : ICollectionFixture<PostgresFixture> { }
// END-TEST::BE-673::2026-03-17::AHL::Fixture compartido de Testcontainers PostgreSQL para tests de integración
