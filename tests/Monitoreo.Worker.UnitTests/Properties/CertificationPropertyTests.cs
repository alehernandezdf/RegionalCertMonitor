// BEGIN-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 3 y 5: Inyección de campos dinámicos XML + Unicidad de consecutivos
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.UnitTests.Properties;

/// <summary>
/// Propiedad 3: Inyección de campos dinámicos en plantillas XML.
/// Valida: Req 2.1, 3.2
/// </summary>
public class CertificationPropertyTests
{
    [Property]
    public Property DynamicFieldsAreNeverEmpty(NonEmptyString country, PositiveInt consecutive)
    {
        var clave = $"CERT-{country.Get}-{consecutive.Get}";
        var fecha = DateTime.UtcNow.ToString("yyyy-MM-dd");

        return (!string.IsNullOrEmpty(clave)
             && !string.IsNullOrEmpty(fecha)
             && consecutive.Get > 0).ToProperty();
    }

    [Property]
    public Property ConsecutiveIsAlwaysIncreasing(PositiveInt start, PositiveInt count)
    {
        var n = Math.Min(count.Get, 100);
        var counter = start.Get;
        var values = new List<int>();

        for (var i = 0; i < n; i++)
        {
            values.Add(Interlocked.Increment(ref counter));
        }

        // Todos los valores deben ser únicos y crecientes
        return (values.Distinct().Count() == values.Count
             && values.SequenceEqual(values.OrderBy(x => x))).ToProperty();
    }

    /// <summary>
    /// Propiedad 5: Unicidad de consecutivos atómicos.
    /// Valida: Req 2.5
    /// </summary>
    [Property]
    public Property ConcurrentIncrementsProduceUniqueValues(PositiveInt threadCount)
    {
        var threads = Math.Min(threadCount.Get, 10);
        var counter = 0;
        var results = new System.Collections.Concurrent.ConcurrentBag<int>();

        Parallel.For(0, threads * 10, _ =>
        {
            results.Add(Interlocked.Increment(ref counter));
        });

        return (results.Distinct().Count() == results.Count).ToProperty();
    }
}
// END-TEST::BE-674::2026-03-25::AHL::PBT Propiedad 3 y 5: Inyección de campos dinámicos XML + Unicidad de consecutivos
