// BEGIN-FEAT::BE-664::2026-03-17::AHL::Record de resultado de monitoreo con todos los campos de persistencia
namespace Monitoreo.Worker.Models;

public record MonitoringResult(
    Guid Id,
    string Country,
    CertificationType CertificationType,
    string Endpoint,
    long TransactionTimeMs,
    bool ResultStatus,
    string? EventErrorMessage,
    DateTimeOffset CreatedAt,
    // BEGIN-FEAT::BE-672::2026-06-01::AHL::Respuesta completa del servicio (sin truncar) para notificaciones por correo
    string? RawResponse = null);
    // END-FEAT::BE-672::2026-06-01::AHL::Respuesta completa del servicio (sin truncar) para notificaciones por correo
// END-FEAT::BE-664::2026-03-17::AHL::Record de resultado de monitoreo con todos los campos de persistencia
