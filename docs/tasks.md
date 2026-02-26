# Tareas de Implementación - Servicio Unificado de Monitoreo

## 1. Estructura del Proyecto y Configuración Base
- [ ] 1.1 Crear solución .NET 8+ con proyectos: `Monitoreo.Worker`, `Monitoreo.Worker.UnitTests`, `Monitoreo.Worker.IntegrationTests`
- [ ] 1.2 Configurar paquetes NuGet base: `Microsoft.Extensions.Hosting`, `Serilog`, `Serilog.Sinks.Console`, `Polly`, `Npgsql`, `AWSSDK.SimpleSystemsManagement`, `AWSSDK.SecretsManager`
- [ ] 1.3 Crear `Program.cs` con configuración de Host, DI, Serilog y registro de servicios
- [ ] 1.4 Crear archivos `appsettings.json` y `appsettings.{PAIS}.json` (GT, SV, DO, CR, PA) para desarrollo local
- [ ] 1.5 Crear `.gitignore` para .NET (excluir bin/, obj/, .vs/, secrets, etc.)

## 2. Modelos de Dominio
- [ ] 2.1 Crear `Models/MonitoringResult.cs` (record con Id, Country, CertificationType, Endpoint, TransactionTimeMs, ResultStatus, EventErrorMessage, CreatedAt)
- [ ] 2.2 Crear `Models/CountryConfig.cs` (record con todos los campos incluyendo NotificationsEmailEnabled, NotificationsWhatsAppEnabled, NotificationCooldownMinutes)
- [ ] 2.3 Crear `Models/CertificationType.cs` (enum ASMX, NUC)
- [ ] 2.4 Crear `Models/NotificationPayload.cs` (record con Result, Type, Recipients) y `NotificationType` enum
- [ ] 2.5 Crear `Models/NotificationGateResult.cs` (record IsAllowed, SuppressedReason) y `NotificationChannel` enum

## 3. Configuración y Secretos (Req 8)
- [ ] 3.1 Crear `Services/Configuration/IConfigurationProvider.cs` con métodos LoadAllCountriesAsync y LoadCountryAsync
- [ ] 3.2 Crear `Services/Configuration/AwsConfigurationProvider.cs` que lea SSM Parameter Store con jerarquía `/monitoreo/{ambiente}/{pais}/` y Secrets Manager
- [ ] 3.3 Implementar fallback a `appsettings.{PAIS}.json` para desarrollo local cuando SSM no está disponible
- [ ] 3.4 Implementar validación de campos obligatorios de CountryConfig con mensajes descriptivos sin exponer credenciales
- [ ] 3.5 Implementar enmascaramiento de credenciales en logs (Serilog destructuring policy)

## 4. Persistencia PostgreSQL (Req 4)
- [ ] 4.1 Crear `Database/init.sql` con tabla `monitoring_results`, índices y vista `monitoring_summary`
- [ ] 4.2 Crear `Services/Persistence/IMonitoringRepository.cs` con WriteResultAsync y GetRecentResultsAsync
- [ ] 4.3 Crear `Services/Persistence/PostgresMonitoringRepository.cs` con Npgsql, connection pooling y parámetros tipados
- [ ] 4.4 Configurar connection string desde variable de ambiente o Secrets Manager

## 5. Servicios de Certificación (Req 2, 3)
- [ ] 5.1 Crear `Services/Certification/ICertificationService.cs` con CertifyAsync y propiedad Type
- [ ] 5.2 Crear `Services/Certification/AsmxCertificationService.cs`: lectura de plantilla XML, inyección de campos dinámicos, envío SOAP via HttpClient, medición con Stopwatch, consecutivo atómico con Interlocked.Increment
- [ ] 5.3 Crear `Services/Certification/NucCertificationService.cs`: obtención de token, preparación XML NUC, POST con Authorization header, parsing JSON de respuesta, consecutivo atómico
- [ ] 5.4 Crear plantillas XML de ejemplo en `Templates/{PAIS}/asmx-template.xml` y `nuc-template.xml`
- [ ] 5.5 Configurar HttpClient con Polly: retry con backoff exponencial, circuit breaker por endpoint, timeouts explícitos

## 6. Servicios de Notificación (Req 5, 6)
- [ ] 6.1 Crear `Services/Notification/INotificationService.cs` con NotifyAsync
- [ ] 6.2 Crear `Services/Notification/EmailNotificationService.cs` con AWSSDK.SimpleEmailV2, construcción de email con país/tipo/tiempo/error/timestamp
- [ ] 6.3 Crear `Services/Notification/WhatsAppNotificationService.cs` con HttpClient, serialización JSON estructurada (System.Text.Json), template monitoring_response_mp

## 7. Control Manual de Notificaciones (Req 7)
- [ ] 7.1 Crear `Services/Notification/INotificationGateService.cs` con EvaluateAsync(countryCode, certType, channel)
- [ ] 7.2 Crear `Services/Notification/NotificationGateService.cs`: lectura de kill switch global desde SSM (sin cache), verificación de flags por país/canal desde CountryConfig, lógica de cooldown con ConcurrentDictionary
- [ ] 7.3 Implementar lectura del parámetro SSM `/monitoreo/{ambiente}/global/notifications-enabled` en cada evaluación para detectar cambios sin reiniciar
- [ ] 7.4 Implementar logging de notificaciones suprimidas con razón (país, tipo, canal, motivo de supresión)

## 8. Orquestación y Workers (Req 1)
- [ ] 8.1 Crear `Services/Orchestration/IMonitoringOrchestrator.cs` con ExecuteCycleAsync
- [ ] 8.2 Crear `Services/Orchestration/MonitoringOrchestrator.cs`: ejecutar certificaciones ASMX+NUC, persistir resultados, evaluar NotificationGate antes de notificar, logging estructurado
- [ ] 8.3 Crear `Workers/CountryMonitoringWorker.cs` como BackgroundService con PeriodicTimer por país, manejo de errores sin detener ciclo
- [ ] 8.4 Registrar un CountryMonitoringWorker por cada país habilitado en DI

## 9. Resiliencia (Req 9)
- [ ] 9.1 Configurar políticas Polly en HttpClientFactory: retry con backoff exponencial para ASMX, NUC, WhatsApp, SES
- [ ] 9.2 Configurar Circuit Breaker por endpoint (umbral 5 fallos, duración 30s, half-open 1 intento) — valores configurables
- [ ] 9.3 Configurar timeouts explícitos por tipo de operación

## 10. Observabilidad y Logging (Req 10)
- [ ] 10.1 Configurar Serilog con sink de CloudWatch, log group `/ecs/monitoreo-unificado/{ambiente}`, formato JSON estructurado
- [ ] 10.2 Implementar enrichers de contexto (Country, CertificationType, CorrelationId) en cada ciclo de monitoreo
- [ ] 10.3 Implementar publicación de métricas custom en CloudWatch (namespace Digifact/Monitoreo): transaction_time_ms, success_count, failure_count, circuit_breaker_state
- [ ] 10.4 Crear `Health/MonitoringHealthCheck.cs` con verificación de conectividad PostgreSQL

## 11. Retención de Datos (Req 15)
- [ ] 11.1 Crear `Services/Retention/DataRetentionService.cs` como BackgroundService con schedule configurable (default: 2 AM diario)
- [ ] 11.2 Implementar DELETE de registros con created_at > 365 días, logging de cantidad eliminada, manejo de errores sin afectar monitoreo

## 12. Dashboards Grafana (Req 11)
- [ ] 12.1 Crear `Grafana/dashboard.json` con paneles: disponibilidad por país, tiempos de respuesta (series de tiempo), alertas activas, tendencias (promedios hora/día)
- [ ] 12.2 Incluir variables de filtro: país (GT, SV, DO, CR, PA), tipo (ASMX, NUC), rango de tiempo
- [ ] 12.3 Configurar datasource PostgreSQL en el dashboard JSON

## 13. Herramienta Visual Local (Req 14)
- [ ] 13.1 Crear `Queries/test-queries.sql` con queries predefinidas: últimos resultados por país, promedios por tipo, fallos recientes

## 14. Docker Compose y Despliegue (Req 12)
- [ ] 14.1 Crear `Dockerfile` multi-stage optimizado basado en .NET 8+ runtime (< 200MB)
- [ ] 14.2 Crear `docker-compose.yml` con Worker Service, PostgreSQL 16 (volumen persistente, health check, init.sql), pgAdmin preconfigurado
- [ ] 14.3 Crear `pgadmin-servers.json` con conexión preconfigurada a PostgreSQL

## 15. CI/CD (Req 12)
- [ ] 15.1 Crear `.github/workflows/ci-cd.yml` con build, tests, publicación de imagen Docker en ECR
- [ ] 15.2 Configurar bloqueo de merge en PR cuando tests o build fallan

## 16. CDK Stack Opcional (Req 12)
- [ ] 16.1 Crear `Monitoreo.Infrastructure/MonitoreoStack.cs` con ECS Fargate, RDS PostgreSQL, ECR, CloudWatch, roles IAM, Security Groups
- [ ] 16.2 Configurar auto-scaling ECS basado en CPU (target 70%, min 1, max 3 tareas)

## 17. Tests Unitarios (Req 13)
- [ ] 17.1 Configurar proyecto de tests con paquetes: xUnit, Moq, FsCheck, FsCheck.Xunit, FluentAssertions
- [ ] 17.2 Crear `MonitoringArbitraries.cs` con generadores FsCheck custom para MonitoringResult y CountryConfig (incluyendo campos de notificación)
- [ ] 17.3 Crear tests unitarios para AsmxCertificationService (mock HttpClient, XML, medición tiempo)
- [ ] 17.4 Crear tests unitarios para NucCertificationService (mock HttpClient, login + cert, parsing JSON)
- [ ] 17.5 Crear tests unitarios para EmailNotificationService (mock SES client, destinatarios, contenido)
- [ ] 17.6 Crear tests unitarios para WhatsAppNotificationService (mock HttpClient, payload JSON, template)
- [ ] 17.7 Crear tests unitarios para NotificationGateService (kill switch, flags por país/canal, cooldown, logging supresiones)
- [ ] 17.8 Crear tests unitarios para MonitoringOrchestrator (flujo completo con mock de gate, notificaciones condicionales)
- [ ] 17.9 Crear tests unitarios para validación de CountryConfig (campos obligatorios, campos de notificación)

## 18. Property-Based Tests (Req 13, Propiedades de Correctitud)
- [ ] 18.1 [PBT] Propiedad 1: Solo países habilitados se programan — `ConfigurationPropertyTests.cs`
- [ ] 18.2 [PBT] Propiedad 2: Cada ciclo produce ambos tipos de certificación — `MonitoringOrchestratorPropertyTests.cs`
- [ ] 18.3 [PBT] Propiedad 3: Inyección de campos dinámicos en plantillas XML — `CertificationPropertyTests.cs`
- [ ] 18.4 [PBT] Propiedad 4: Invariante de construcción de MonitoringResult — `MonitoringResultPropertyTests.cs`
- [ ] 18.5 [PBT] Propiedad 5: Unicidad de consecutivos atómicos — `CertificationPropertyTests.cs`
- [ ] 18.6 [PBT] Propiedad 6: Round-trip de persistencia en PostgreSQL — `PostgresPropertyTests.cs`
- [ ] 18.7 [PBT] Propiedad 7: Lógica de disparo de notificaciones con control manual — `NotificationPropertyTests.cs`
- [ ] 18.8 [PBT] Propiedad 8: Completitud y validez del contenido de notificaciones — `NotificationPropertyTests.cs`
- [ ] 18.9 [PBT] Propiedad 9: Enmascaramiento de credenciales en logs — `SecurityPropertyTests.cs`
- [ ] 18.10 [PBT] Propiedad 10: Validación de CountryConfig — `ConfigurationPropertyTests.cs`
- [ ] 18.11 [PBT] Propiedad 11: Completitud de logs estructurados — `LoggingPropertyTests.cs`
- [ ] 18.12 [PBT] Propiedad 12: Correctitud de retención de datos — `RetentionPropertyTests.cs`
- [ ] 18.13 [PBT] Propiedad 13: Cooldown de notificaciones previene spam — `NotificationGatePropertyTests.cs`
- [ ] 18.14 [PBT] Propiedad 14: Monitoreo continúa independiente de flags de notificación — `NotificationGatePropertyTests.cs`

## 19. Tests de Integración (Req 13)
- [ ] 19.1 Configurar Testcontainers.PostgreSql para tests de integración
- [ ] 19.2 Crear `PostgresIntegrationTests.cs`: escritura/lectura real, creación de tabla/índices, vista monitoring_summary
- [ ] 19.3 Crear `MonitoringFlowIntegrationTests.cs`: ciclo completo con mocks de endpoints externos y PostgreSQL real
