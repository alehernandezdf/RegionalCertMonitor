// BEGIN-TEST::BE-664::2026-03-17::AHL::Tests de integración PostgreSQL: escritura/lectura real, tabla, índices, vista monitoring_summary
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Persistence;
using Moq;
using Npgsql;

namespace Monitoreo.Worker.IntegrationTests;

[Collection("Postgres")]
public class PostgresIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public PostgresIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private PostgresMonitoringRepository CreateRepo()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSQL"] = _fixture.ConnectionString
            })
            .Build();
        return new PostgresMonitoringRepository(config, new Mock<ILogger<PostgresMonitoringRepository>>().Object);
    }

    private static MonitoringResult MakeResult(string country = "GT", bool success = true) => new(
        Guid.NewGuid(), country, CertificationType.ASMX,
        "https://asmx.test/cert", 1500, success,
        success ? null : "Test error", DateTimeOffset.UtcNow);

    [Fact]
    public async Task WriteAndRead_RoundTrip_ReturnsInsertedResult()
    {
        var repo = CreateRepo();
        var original = MakeResult();

        await repo.WriteResultAsync(original, CancellationToken.None);
        var results = await repo.GetRecentResultsAsync("GT", 10, CancellationToken.None);

        results.Should().ContainSingle(r => r.Id == original.Id);
        var found = results.First(r => r.Id == original.Id);
        found.Country.Should().Be("GT");
        found.CertificationType.Should().Be(CertificationType.ASMX);
        found.TransactionTimeMs.Should().Be(1500);
        found.ResultStatus.Should().BeTrue();
    }

    [Fact]
    public async Task WriteResult_WithError_PersistsErrorMessage()
    {
        var repo = CreateRepo();
        var result = MakeResult(success: false);

        await repo.WriteResultAsync(result, CancellationToken.None);
        var results = await repo.GetRecentResultsAsync("GT", 10, CancellationToken.None);

        var found = results.First(r => r.Id == result.Id);
        found.ResultStatus.Should().BeFalse();
        found.EventErrorMessage.Should().Be("Test error");
    }

    [Fact]
    public async Task GetRecentResults_RespectsLimit()
    {
        var repo = CreateRepo();
        for (int i = 0; i < 5; i++)
            await repo.WriteResultAsync(MakeResult("SV"), CancellationToken.None);

        var results = await repo.GetRecentResultsAsync("SV", 3, CancellationToken.None);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRecentResults_FiltersbyCountry()
    {
        var repo = CreateRepo();
        await repo.WriteResultAsync(MakeResult("CR"), CancellationToken.None);
        await repo.WriteResultAsync(MakeResult("PA"), CancellationToken.None);

        var crResults = await repo.GetRecentResultsAsync("CR", 10, CancellationToken.None);
        var paResults = await repo.GetRecentResultsAsync("PA", 10, CancellationToken.None);

        crResults.Should().OnlyContain(r => r.Country == "CR");
        paResults.Should().OnlyContain(r => r.Country == "PA");
    }

    [Fact]
    public async Task MonitoringSummaryView_ReturnsAggregatedData()
    {
        var repo = CreateRepo();
        await repo.WriteResultAsync(MakeResult("DO", true), CancellationToken.None);
        await repo.WriteResultAsync(MakeResult("DO", false), CancellationToken.None);

        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT country, total_checks, success_count, failure_count FROM monitoring_summary WHERE country = 'DO'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            reader.GetString(0).Should().Be("DO");
            reader.GetInt64(1).Should().BeGreaterThanOrEqualTo(2);
        }
    }

    [Fact]
    public async Task TableIndices_Exist()
    {
        await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT indexname FROM pg_indexes WHERE tablename = 'monitoring_results' ORDER BY indexname", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var indices = new List<string>();
        while (await reader.ReadAsync())
            indices.Add(reader.GetString(0));

        indices.Should().Contain("idx_monitoring_results_country");
        indices.Should().Contain("idx_monitoring_results_created_at");
        indices.Should().Contain("idx_monitoring_results_country_type_created");
    }
}
// END-TEST::BE-664::2026-03-17::AHL::Tests de integración PostgreSQL: escritura/lectura real, tabla, índices, vista monitoring_summary
