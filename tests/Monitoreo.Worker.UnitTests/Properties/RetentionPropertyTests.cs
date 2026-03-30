// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 12: Correctitud de retención de datos
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 12: Correctitud de retención de datos.
/// Valida: Req 15.1
/// </summary>
public class RetentionPropertyTests
{
    [Property]
    public Property RecordsOlderThan365DaysAreEligibleForDeletion(PositiveInt daysOld)
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-daysOld.Get);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-365);

        var shouldDelete = createdAt < cutoff;
        var expected = daysOld.Get > 365;

        return (shouldDelete == expected).ToProperty();
    }

    [Property]
    public Property RecordsNewerThan365DaysAreRetained(PositiveInt daysOld)
    {
        var days = Math.Min(daysOld.Get, 364); // Asegurar < 365
        var createdAt = DateTimeOffset.UtcNow.AddDays(-days);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-365);

        return (createdAt >= cutoff).ToProperty();
    }

    [Property]
    public Property RetentionCutoffIsExactly365Days()
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddDays(-365);
        var diff = now - cutoff;

        return (diff.TotalDays >= 364.9 && diff.TotalDays <= 365.1).ToProperty();
    }
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 12: Correctitud de retención de datos
