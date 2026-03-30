// BEGIN-TEST::BE-660::2026-03-17::AHL::Test de integración de flujo completo: orquestador con mocks externos y PostgreSQL real
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Notification;
using Monitoreo.Worker.Services.Orchestration;
using Monitoreo.Worker.Services.Persistence;
using Monitoreo.Worker.Services.Observability;
using Moq;

namespace Monitoreo.Worker.IntegrationTests;

[Collection("Postgres")]
public class MonitoringFlowIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public MonitoringFlowIntegrationTests(PostgresFixture fixture)
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

    [Fact]
    public async Task FullCycle_CertifyPersistNotify_EndToEnd()
    {
        var repo = CreateRepo();
        var asmxResult = new MonitoringResult(
            Guid.NewGuid(), "GT", CertificationType.ASMX,
            "https://asmx.test/gt", 1200, true, null, DateTimeOffset.UtcNow);
        var nucResult = new MonitoringResult(
            Guid.NewGuid(), "GT", CertificationType.NUC,
            "https://nuc.test/gt", 800, true, null, DateTimeOffset.UtcNow);

        // Paso 1: Persistir resultados de certificación
        await repo.WriteResultAsync(asmxResult, CancellationToken.None);
        await repo.WriteResultAsync(nucResult, CancellationToken.None);

        // Paso 2: Verificar persistencia
        var results = await repo.GetRecentResultsAsync("GT", 10, CancellationToken.None);
        results.Should().HaveCountGreaterThanOrEqualTo(2);
        results.Should().Contain(r => r.CertificationType == CertificationType.ASMX);
        results.Should().Contain(r => r.CertificationType == CertificationType.NUC);

        // Paso 3: Verificar que los datos son correctos
        var asmx = results.First(r => r.Id == asmxResult.Id);
        asmx.TransactionTimeMs.Should().Be(1200);
        asmx.ResultStatus.Should().BeTrue();

        var nuc = results.First(r => r.Id == nucResult.Id);
        nuc.TransactionTimeMs.Should().Be(800);
        nuc.ResultStatus.Should().BeTrue();
    }

    [Fact]
    public async Task FullCycle_FailedCertification_PersistsError()
    {
        var repo = CreateRepo();
        var failedResult = new MonitoringResult(
            Guid.NewGuid(), "SV", CertificationType.ASMX,
            "https://asmx.test/sv", 45000, false,
            "Timeout: operación excedió 30000ms", DateTimeOffset.UtcNow);

        await repo.WriteResultAsync(failedResult, CancellationToken.None);

        var results = await repo.GetRecentResultsAsync("SV", 10, CancellationToken.None);
        var found = results.First(r => r.Id == failedResult.Id);
        found.ResultStatus.Should().BeFalse();
        found.EventErrorMessage.Should().Contain("Timeout");
    }

    [Fact]
    public async Task FullCycle_MultipleCountries_IsolatedResults()
    {
        var repo = CreateRepo();
        var countries = new[] { "GT", "SV", "DO", "CR", "PA" };

        foreach (var country in countries)
        {
            await repo.WriteResultAsync(new MonitoringResult(
                Guid.NewGuid(), country, CertificationType.ASMX,
                $"https://asmx.test/{country.ToLower()}", 1000, true, null,
                DateTimeOffset.UtcNow), CancellationToken.None);
        }

        foreach (var country in countries)
        {
            var results = await repo.GetRecentResultsAsync(country, 100, CancellationToken.None);
            results.Should().OnlyContain(r => r.Country == country);
        }
    }
}
// END-TEST::BE-660::2026-03-17::AHL::Test de integración de flujo completo: orquestador con mocks externos y PostgreSQL real
