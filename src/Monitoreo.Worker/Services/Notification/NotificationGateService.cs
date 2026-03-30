// BEGIN-FEAT::BE-667::2026-03-17::AHL::Gate de notificaciones con kill switch global SSM, flags por país/canal y cooldown con ConcurrentDictionary
using System.Collections.Concurrent;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Monitoreo.Worker.Models;
using Monitoreo.Worker.Services.Configuration;

namespace Monitoreo.Worker.Services.Notification;

public class NotificationGateService : INotificationGateService
{
    private readonly IAmazonSimpleSystemsManagement _ssm;
    private readonly Configuration.IConfigurationProvider _configProvider;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastNotificationTimes = new();
    private readonly ILogger<NotificationGateService> _logger;
    private readonly IConfiguration _configuration;

    public NotificationGateService(
        IAmazonSimpleSystemsManagement ssm,
        Configuration.IConfigurationProvider configProvider,
        IConfiguration configuration,
        ILogger<NotificationGateService> logger)
    {
        _ssm = ssm;
        _configProvider = configProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<NotificationGateResult> EvaluateAsync(
        string countryCode, string certType, NotificationChannel channel, CancellationToken ct)
    {
        // 1. Kill switch global desde SSM (sin cache)
        if (!await IsGloballyEnabledAsync(ct))
        {
            _logger.LogInformation("Notificación suprimida: kill switch global desactivado ({Country}/{CertType}/{Channel})",
                countryCode, certType, channel);
            return new NotificationGateResult(false, "Kill switch global desactivado");
        }

        // 2. Verificar flag del canal para el país
        var config = await _configProvider.LoadCountryAsync(countryCode, ct);

        if (channel == NotificationChannel.Email && !config.NotificationsEmailEnabled)
        {
            _logger.LogInformation("Notificación suprimida: Email deshabilitado para {Country}", countryCode);
            return new NotificationGateResult(false, $"Email deshabilitado para {countryCode}");
        }

        if (channel == NotificationChannel.WhatsApp && !config.NotificationsWhatsAppEnabled)
        {
            _logger.LogInformation("Notificación suprimida: WhatsApp deshabilitado para {Country}", countryCode);
            return new NotificationGateResult(false, $"WhatsApp deshabilitado para {countryCode}");
        }

        // 3. Verificar cooldown
        var key = $"{countryCode}_{certType}_{channel}";
        if (_lastNotificationTimes.TryGetValue(key, out var lastTime))
        {
            var elapsed = DateTimeOffset.UtcNow - lastTime;
            var cooldown = TimeSpan.FromMinutes(config.NotificationCooldownMinutes);

            if (elapsed < cooldown)
            {
                var remaining = cooldown - elapsed;
                _logger.LogInformation(
                    "Notificación suprimida: cooldown activo ({Remaining:F0} min restantes) para {Key}",
                    remaining.TotalMinutes, key);
                return new NotificationGateResult(false,
                    $"Cooldown activo ({remaining.TotalMinutes:F0} min restantes)");
            }
        }

        // 4. Permitido — actualizar timestamp
        _lastNotificationTimes[key] = DateTimeOffset.UtcNow;
        return new NotificationGateResult(true, null);
    }

    private async Task<bool> IsGloballyEnabledAsync(CancellationToken ct)
    {
        var env = _configuration["Monitoring:Environment"] ?? "Development";
        var paramName = $"/monitoreo/{env}/global/notifications-enabled";

        try
        {
            var response = await _ssm.GetParameterAsync(
                new GetParameterRequest { Name = paramName }, ct);
            return bool.TryParse(response.Parameter.Value, out var enabled) && enabled;
        }
        catch (ParameterNotFoundException)
        {
            _logger.LogWarning("Parámetro SSM {ParamName} no encontrado, asumiendo habilitado", paramName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error leyendo kill switch SSM, asumiendo habilitado");
            return true;
        }
    }
}
// END-FEAT::BE-667::2026-03-17::AHL::Gate de notificaciones con kill switch global SSM, flags por país/canal y cooldown con ConcurrentDictionary
