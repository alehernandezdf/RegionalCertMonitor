// BEGIN-FEAT::BE-668::2026-03-17::AHL::Interfaz de proveedor de configuración multi-país
using Monitoreo.Worker.Models;

namespace Monitoreo.Worker.Services.Configuration;

public interface IConfigurationProvider
{
    Task<IReadOnlyList<CountryConfig>> LoadAllCountriesAsync(CancellationToken ct);
    Task<CountryConfig> LoadCountryAsync(string countryCode, CancellationToken ct);
}
// END-FEAT::BE-668::2026-03-17::AHL::Interfaz de proveedor de configuración multi-país
