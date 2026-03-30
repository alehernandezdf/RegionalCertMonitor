// FEAT::BE-662::2026-03-17::AHL::Record de resultado CUFE con JWT y XML actualizado (PA)
namespace Monitoreo.Worker.Models;

public record CufeResult(string Cufe, string Jwt, string UpdatedXml);
