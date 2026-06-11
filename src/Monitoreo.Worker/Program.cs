// BEGIN-FEAT::BE-660::2026-03-25::AHL::Configuracion de Host, DI, Serilog con modo Development sin AWS
using Serilog;
using Serilog.Events;
using Monitoreo.Worker.Services.Configuration;
using Monitoreo.Worker.Services.Persistence;
using Monitoreo.Worker.Services.Certification;
using Monitoreo.Worker.Services.Notification;
using Monitoreo.Worker.Services.Orchestration;
using Monitoreo.Worker.Services.Retention;
using Monitoreo.Worker.Services.Observability;
using Monitoreo.Worker.Workers;
using Amazon.SimpleSystemsManagement;
using Amazon.SecretsManager;
using Amazon.SimpleEmailV2;
using Polly;
using Polly.Extensions.Http;
using Amazon.CloudWatch;
using Amazon.CloudWatchLogs;
using Serilog.Sinks.AwsCloudWatch;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{Country}/{CertificationType}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Iniciando Servicio Unificado de Monitoreo - Digifact");

    var builder = Host.CreateApplicationBuilder(args);

    // Cargar configuracion por pais y secretos para desarrollo local
    builder.Configuration
        .AddJsonFile("appsettings.GT.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.SV.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.DO.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.CR.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.PA.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.GT2.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

    var monitoringEnv = builder.Configuration["Monitoring:Environment"] ?? "Development";
    var isDevelopment = string.Equals(monitoringEnv, "Development", StringComparison.OrdinalIgnoreCase);

    // Serilog (Console sink ya viene de appsettings.json - NO duplicar aqui)
    builder.Services.AddSerilog((services, lc) =>
    {
        lc.ReadFrom.Configuration(builder.Configuration)
          .ReadFrom.Services(services)
          .Enrich.FromLogContext()
          .Enrich.WithProperty("Application", "MonitoreoUnificado")
          .Enrich.WithProperty("Environment", monitoringEnv);

        if (!isDevelopment)
        {
            var cloudWatchClient = new AmazonCloudWatchLogsClient();
            lc.WriteTo.AmazonCloudWatch(
                logGroup: $"/ecs/monitoreo-unificado/{monitoringEnv.ToLowerInvariant()}",
                logStreamPrefix: "worker-",
                cloudWatchClient: cloudWatchClient,
                textFormatter: new Serilog.Formatting.Json.JsonFormatter());
        }
    });

    // DI condicional: Development usa stubs locales, produccion usa AWS
    if (isDevelopment)
    {
        var dummyCreds = new Amazon.Runtime.BasicAWSCredentials("dev", "dev");
        var region = Amazon.RegionEndpoint.USEast1;
        builder.Services.AddSingleton<IAmazonSimpleSystemsManagement>(_ => new AmazonSimpleSystemsManagementClient(dummyCreds, region));
        builder.Services.AddSingleton<IAmazonSecretsManager>(_ => new AmazonSecretsManagerClient(dummyCreds, region));

        // Notificaciones: gate local (sin SSM) + email real via SMTP + WhatsApp real, metricas a consola
        builder.Services.AddSingleton<INotificationGateService, LocalNotificationGateService>();
        builder.Services.AddSingleton<EmailNotificationService>();
        builder.Services.AddSingleton<WhatsAppNotificationService>();
        builder.Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<EmailNotificationService>());
        builder.Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<WhatsAppNotificationService>());
        builder.Services.AddSingleton<IMetricsPublisher, ConsoleMetricsPublisher>();
    }
    else
    {
        builder.Services.AddSingleton<IAmazonSimpleSystemsManagement, AmazonSimpleSystemsManagementClient>();
        builder.Services.AddSingleton<IAmazonSecretsManager, AmazonSecretsManagerClient>();
        builder.Services.AddSingleton<IAmazonSimpleEmailServiceV2, AmazonSimpleEmailServiceV2Client>();
        builder.Services.AddSingleton<IAmazonCloudWatch, AmazonCloudWatchClient>();

        builder.Services.AddSingleton<INotificationGateService, NotificationGateService>();
        builder.Services.AddSingleton<EmailNotificationService>();
        builder.Services.AddSingleton<WhatsAppNotificationService>();
        builder.Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<EmailNotificationService>());
        builder.Services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<WhatsAppNotificationService>());
        builder.Services.AddSingleton<IMetricsPublisher, CloudWatchMetricsPublisher>();
    }

    // Configuration provider (fallback a appsettings en Development)
    builder.Services.AddSingleton<Monitoreo.Worker.Services.Configuration.IConfigurationProvider, AwsConfigurationProvider>();

    // Persistence
    builder.Services.AddSingleton<IMonitoringRepository, PostgresMonitoringRepository>();
    builder.Services.AddSingleton<ISequentialCounterService, PostgresSequentialCounterService>();

    // Certification services
    builder.Services.AddSingleton<IPfxSigningService, PfxSigningService>();
    builder.Services.AddSingleton<IQrGenerationService, QrGenerationService>();
    builder.Services.AddSingleton<ICufeGenerationService, CufeGenerationService>();
    builder.Services.AddSingleton<IAsmxPreProcessingPipeline, AsmxPreProcessingPipeline>();
    builder.Services.AddSingleton<AsmxCertificationService>();
    builder.Services.AddSingleton<NucCertificationService>();
    builder.Services.AddSingleton<ApiFelCertificationService>();
    builder.Services.AddSingleton<ICertificationService>(sp => sp.GetRequiredService<AsmxCertificationService>());
    builder.Services.AddSingleton<ICertificationService>(sp => sp.GetRequiredService<NucCertificationService>());
    builder.Services.AddSingleton<ICertificationService>(sp => sp.GetRequiredService<ApiFelCertificationService>());

    // Orchestration
    builder.Services.AddSingleton<IMonitoringOrchestrator, MonitoringOrchestrator>();

    // Data retention
    builder.Services.AddHostedService<DataRetentionService>();

    // HttpClient con Polly: retry exponencial, circuit breaker, timeouts
    var resilienceConfig = builder.Configuration.GetSection("Resilience");

    builder.Services.AddHttpClient("AsmxClient")
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
            resilienceConfig.GetValue("Asmx:RetryCount", 3),
            attempt => TimeSpan.FromSeconds(
                Math.Pow(resilienceConfig.GetValue("Asmx:RetryBaseDelaySeconds", 2.0), attempt))))
        .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(
            resilienceConfig.GetValue("Asmx:CircuitBreakerFailureThreshold", 5),
            TimeSpan.FromSeconds(resilienceConfig.GetValue("Asmx:CircuitBreakerDurationSeconds", 30))))
        .ConfigureHttpClient(c =>
            c.Timeout = TimeSpan.FromSeconds(resilienceConfig.GetValue("Asmx:TimeoutSeconds", 30)));

    builder.Services.AddHttpClient("NucClient")
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
            resilienceConfig.GetValue("Nuc:RetryCount", 3),
            attempt => TimeSpan.FromSeconds(
                Math.Pow(resilienceConfig.GetValue("Nuc:RetryBaseDelaySeconds", 2.0), attempt))))
        .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(
            resilienceConfig.GetValue("Nuc:CircuitBreakerFailureThreshold", 5),
            TimeSpan.FromSeconds(resilienceConfig.GetValue("Nuc:CircuitBreakerDurationSeconds", 30))))
        .ConfigureHttpClient(c =>
            c.Timeout = TimeSpan.FromSeconds(resilienceConfig.GetValue("Nuc:TimeoutSeconds", 30)));

    builder.Services.AddHttpClient("WhatsAppClient")
        .AddTransientHttpErrorPolicy(p => p.WaitAndRetryAsync(
            resilienceConfig.GetValue("WhatsApp:RetryCount", 2),
            attempt => TimeSpan.FromSeconds(
                Math.Pow(resilienceConfig.GetValue("WhatsApp:RetryBaseDelaySeconds", 1.0), attempt))))
        .AddTransientHttpErrorPolicy(p => p.CircuitBreakerAsync(
            resilienceConfig.GetValue("WhatsApp:CircuitBreakerFailureThreshold", 5),
            TimeSpan.FromSeconds(resilienceConfig.GetValue("WhatsApp:CircuitBreakerDurationSeconds", 60))))
        .ConfigureHttpClient(c =>
            c.Timeout = TimeSpan.FromSeconds(resilienceConfig.GetValue("WhatsApp:TimeoutSeconds", 15)));

    // Registrar un CountryMonitoringWorker por cada pais habilitado
    // Se usa un HostedService factory que carga la config al iniciar
    builder.Services.AddHostedService<CountryWorkerRegistrar>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "El servicio termino inesperadamente");
}
finally
{
    await Log.CloseAndFlushAsync();
}
// END-FEAT::BE-660::2026-03-25::AHL::Configuracion de Host, DI, Serilog con modo Development sin AWS
