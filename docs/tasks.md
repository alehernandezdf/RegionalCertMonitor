# Tareas de Implementación - Servicio Unificado de Monitoreo

## 1. Estructura del Proyecto y Configuración Base
- [ ] 1.1 Crear solución .NET 8+ con proyectos: `Monitoreo.Worker`, `Monitoreo.Worker.UnitTests`, `Monitoreo.Worker.IntegrationTests`
- [ ] 1.2 Configurar paquetes NuGet base: `Microsoft.Extensions.Hosting`, `Serilog`, `Serilog.Sinks.Console`, `Polly`, `Npgsql`, `AWSSDK.SimpleSystemsManagement`, `AWSSDK.SecretsManager`, `System.Security.Cryptography.Xml`
- [ ] 1.3 Crear `Program.cs` con configuración de Host, DI, Serilog y registro de servicios
- [ ] 1.4 Crear archivos `appsettings.json` y `appsettings.{PAIS}.json` (GT, SV, DO, CR, PA) para desarrollo local
- [ ] 1.5 Crear `appsettings.Secrets.template.json` con la estructura de credenciales (valores vacíos) como referencia para desarrolladores
- [ ] 1.6 Crear `.gitignore` para .NET (excluir bin/, obj/, .vs/, secrets, appsettings.Secrets.json, etc.)

## 2. Modelos de Dominio
- [ ] 2.1 Crear `Models/MonitoringResult.cs` (record con Id, Country, CertificationType, Endpoint, TransactionTimeMs, ResultStatus, EventErrorMessage, CreatedAt)
- [ ] 2.2 Crear `Models/CountryConfig.cs` (record con todos los campos: CountryCode, Enabled, MonitoringInterval, AlertThresholdMs, AsmxEndpoint, NucLoginEndpoint, NucCertEndpoint, AsmxTemplatePath, NucTemplatePath, TaxId, Requestor, NucUsername, NucAuthMode ["dynamic"/"static"], NucUsernameFormat, RequiresPfxSignature [bool, default false], PfxSecretArn [string?], PfxPasswordSecretArn [string?], RequiresQrGeneration [bool, default false], QrCode [string?], RequiresCufe [bool, default false], EmailRecipients, WhatsAppNumbers, NotificationsEmailEnabled, NotificationsWhatsAppEnabled, NotificationCooldownMinutes, NucCredentialSecretArn, WhatsAppTokenSecretArn)
- [ ] 2.3 Crear `Models/CertificationType.cs` (enum ASMX, NUC)
- [ ] 2.4 Crear `Models/NotificationPayload.cs` (record con Result, Type, Recipients) y `NotificationType` enum
- [ ] 2.5 Crear `Models/NotificationGateResult.cs` (record IsAllowed, SuppressedReason) y `NotificationChannel` enum
- [ ] 2.6 Crear `Models/CufeResult.cs` (record con Cufe, Jwt, UpdatedXml)
  - _Requerimientos: Req 2 (AC 2.8)_

## 3. Configuración y Secretos (Req 8)
- [ ] 3.1 Crear `Services/Configuration/IConfigurationProvider.cs` con métodos LoadAllCountriesAsync y LoadCountryAsync
- [ ] 3.2 Crear `Services/Configuration/AwsConfigurationProvider.cs` que lea SSM Parameter Store con jerarquía `/monitoreo/{ambiente}/{pais}/` y Secrets Manager
  - Incluir lectura de todos los parámetros SSM nuevos: tax-id, requestor, nuc-username, nuc-auth-mode, nuc-username-format, pfx-secret-arn, pfx-password-secret-arn, requires-pfx-signature, requires-qr-generation, qr-code, requires-cufe
  - Incluir lectura de secretos PFX (base64) y contraseña PFX desde Secrets Manager
  - Incluir lectura de token NUC estático desde Secrets Manager para modo "static" (GT)
- [ ] 3.3 Implementar fallback de credenciales a `appsettings.Secrets.json` cuando Secrets Manager no está disponible y el ambiente es Development
  - Prioridad: (1) Secrets Manager, (2) appsettings.Secrets.json si Development, (3) error descriptivo
  - _Requerimientos: Req 8 (AC 8.9, 8.10, 8.11)_
- [ ] 3.4 Implementar fallback de configuración no sensible a `appsettings.{PAIS}.json` para desarrollo local cuando SSM no está disponible
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
  - Integrar dependencia de `IAsmxPreProcessingPipeline`: llamar `pipeline.ProcessAsync(xml, config, ct)` antes del envío SOAP
  - Si el pipeline falla, retornar `MonitoringResult` con `ResultStatus=false` sin intentar la certificación ASMX
  - _Requerimientos: Req 2 (AC 2.1, 2.2, 2.3, 2.5, 2.6, 2.7, 2.8)_
- [ ] 5.3 Crear `Services/Certification/NucCertificationService.cs`: preparación XML NUC, POST con Authorization header, parsing JSON de respuesta, consecutivo atómico
  - Implementar soporte dual de autenticación según `NucAuthMode`:
    - Modo "dynamic" (CR, SV, DO, PA): login con credenciales formateadas via `BuildNucUsername` usando `NucUsernameFormat` con interpolación de variables ({Country}, {TaxId}, {NucUsername}, {NRC}, {NIT})
    - Modo "static" (GT): lectura de JWT estático desde Secrets Manager sin ejecutar login
  - _Requerimientos: Req 3 (AC 3.1, 3.2, 3.3, 3.5), Req 9 (AC 9.4, 9.5)_
- [ ] 5.4 Crear plantillas XML de ejemplo en `Templates/{PAIS}/asmx-template.xml` y `nuc-template.xml`
- [ ] 5.5 Configurar HttpClient con Polly: retry con backoff exponencial, circuit breaker por endpoint, timeouts explícitos

## 6. Servicio de Firma PFX (Req 2 — AC 2.6, 2.9)
- [ ] 6.1 Crear `Services/Certification/IPfxSigningService.cs` con método `SignXmlAsync(string xmlContent, string pfxBase64, string pfxPassword, CancellationToken ct)`
- [ ] 6.2 Crear `Services/Certification/PfxSigningService.cs`:
  - Cargar certificado PFX desde base64 usando `X509Certificate2`
  - Firmar XML usando `System.Security.Cryptography.Xml.SignedXml`
  - Retornar XML firmado con elemento `<Signature>` incluido
  - Manejar errores: secreto no encontrado, PFX inválido, certificado expirado, fallo de firma
  - _Requerimientos: Req 2 (AC 2.6, 2.9)_

## 7. Servicio de Generación QR (Req 2 — AC 2.7)
- [ ] 7.1 Crear `Services/Certification/IQrGenerationService.cs` con método `AddQrToXmlAsync(string xmlContent, CountryConfig config, CancellationToken ct)`
- [ ] 7.2 Crear `Services/Certification/QrGenerationService.cs`:
  - Generar código QR (ADDQR) según configuración de `CountryConfig.QrCode`
  - Inyectar resultado en el nodo correspondiente del XML
  - Retornar XML actualizado con QR
  - Solo utilizado por PA
  - _Requerimientos: Req 2 (AC 2.7)_

## 8. Servicio de Generación CUFE (Req 2 — AC 2.8)
- [ ] 8.1 Crear `Services/Certification/ICufeGenerationService.cs` con método `GenerateCufeAsync(string xmlContent, CountryConfig config, CancellationToken ct)`
- [ ] 8.2 Crear `Services/Certification/CufeGenerationService.cs`:
  - Calcular CUFE basado en datos del documento
  - Obtener JWT via llamada HTTP a GetJWT (configurado con Polly)
  - Retornar `CufeResult` (Cufe, Jwt, UpdatedXml)
  - Solo utilizado por PA
  - _Requerimientos: Req 2 (AC 2.8)_

## 9. Pipeline de Pre-Procesamiento ASMX (Req 2)
- [ ] 9.1 Crear `Services/Certification/IAsmxPreProcessingPipeline.cs` con método `ProcessAsync(string xmlContent, CountryConfig config, CancellationToken ct)`
- [ ] 9.2 Crear `Services/Certification/AsmxPreProcessingPipeline.cs`:
  - Inyectar `IPfxSigningService`, `IQrGenerationService`, `ICufeGenerationService`, `IAmazonSecretsManager`
  - Orquestar pasos en orden: PFX signing (PA, DO) → QR generation (PA) → CUFE+JWT (PA)
  - Evaluar flags de `CountryConfig`: `RequiresPfxSignature`, `RequiresQrGeneration`, `RequiresCufe`
  - Si ningún flag está activo (GT, SV, CR), retornar XML sin modificar
  - Si cualquier paso falla, retornar error sin intentar pasos siguientes ni la llamada ASMX
  - Obtener PFX base64 y contraseña desde Secrets Manager usando `PfxSecretArn` y `PfxPasswordSecretArn`
  - _Requerimientos: Req 2 (AC 2.6, 2.7, 2.8)_

## 10. Checkpoint - Verificar servicios de certificación y pre-procesamiento
- [ ] 10.1 Ensure all tests pass, ask the user if questions arise.

## 11. Servicios de Notificación (Req 5, 6)
- [ ] 11.1 Crear `Services/Notification/INotificationService.cs` con NotifyAsync
- [ ] 11.2 Crear `Services/Notification/EmailNotificationService.cs` con AWSSDK.SimpleEmailV2, construcción de email con país/tipo/tiempo/error/timestamp
  - Nota: GT y SV se migran de Gmail SMTP a Amazon SES como parte de la unificación
- [ ] 11.3 Crear `Services/Notification/WhatsAppNotificationService.cs` con HttpClient, serialización JSON estructurada (System.Text.Json), template monitoring_response_mp

## 12. Control Manual de Notificaciones (Req 7)
- [ ] 12.1 Crear `Services/Notification/INotificationGateService.cs` con EvaluateAsync(countryCode, certType, channel)
- [ ] 12.2 Crear `Services/Notification/NotificationGateService.cs`: lectura de kill switch global desde SSM (sin cache), verificación de flags por país/canal desde CountryConfig, lógica de cooldown con ConcurrentDictionary
- [ ] 12.3 Implementar lectura del parámetro SSM `/monitoreo/{ambiente}/global/notifications-enabled` en cada evaluación para detectar cambios sin reiniciar
- [ ] 12.4 Implementar logging de notificaciones suprimidas con razón (país, tipo, canal, motivo de supresión)

## 13. Orquestación y Workers (Req 1)
- [ ] 13.1 Crear `Services/Orchestration/IMonitoringOrchestrator.cs` con ExecuteCycleAsync
- [ ] 13.2 Crear `Services/Orchestration/MonitoringOrchestrator.cs`: ejecutar certificaciones ASMX+NUC, persistir resultados, evaluar NotificationGate antes de notificar, logging estructurado
- [ ] 13.3 Crear `Workers/CountryMonitoringWorker.cs` como BackgroundService con PeriodicTimer por país, manejo de errores sin detener ciclo
- [ ] 13.4 Registrar un CountryMonitoringWorker por cada país habilitado en DI

## 14. Resiliencia (Req 9)
- [ ] 14.1 Configurar políticas Polly en HttpClientFactory: retry con backoff exponencial para ASMX, NUC, WhatsApp, SES
- [ ] 14.2 Configurar Circuit Breaker por endpoint (umbral 5 fallos, duración 30s, half-open 1 intento) — valores configurables
- [ ] 14.3 Configurar timeouts explícitos por tipo de operación

## 15. Observabilidad y Logging (Req 10)
- [ ] 15.1 Configurar Serilog con sink de CloudWatch, log group `/ecs/monitoreo-unificado/{ambiente}`, formato JSON estructurado
- [ ] 15.2 Implementar enrichers de contexto (Country, CertificationType, CorrelationId) en cada ciclo de monitoreo
- [ ] 15.3 Implementar publicación de métricas custom en CloudWatch (namespace Digifact/Monitoreo): transaction_time_ms, success_count, failure_count, circuit_breaker_state
- [ ] 15.4 Crear `Health/MonitoringHealthCheck.cs` con verificación de conectividad PostgreSQL

## 16. Retención de Datos (Req 15)
- [ ] 16.1 Crear `Services/Retention/DataRetentionService.cs` como BackgroundService con schedule configurable (default: 2 AM diario)
- [ ] 16.2 Implementar DELETE de registros con created_at > 365 días, logging de cantidad eliminada, manejo de errores sin afectar monitoreo

## 17. Dashboards Grafana (Req 11)
- [ ] 17.1 Crear `Grafana/dashboard.json` con paneles: disponibilidad por país, tiempos de respuesta (series de tiempo), alertas activas, tendencias (promedios hora/día)
- [ ] 17.2 Incluir variables de filtro: país (GT, SV, DO, CR, PA), tipo (ASMX, NUC), rango de tiempo
- [ ] 17.3 Configurar datasource PostgreSQL en el dashboard JSON

## 18. Herramienta Visual Local (Req 14)
- [ ] 18.1 Crear `Queries/test-queries.sql` con queries predefinidas: últimos resultados por país, promedios por tipo, fallos recientes

## 19. Docker Compose y Despliegue (Req 12)
- [ ] 19.1 Crear `Dockerfile` multi-stage optimizado basado en .NET 8+ runtime (< 200MB)
- [ ] 19.2 Crear `docker-compose.yml` con Worker Service, PostgreSQL 16 (volumen persistente, health check, init.sql), pgAdmin preconfigurado
- [ ] 19.3 Crear `pgadmin-servers.json` con conexión preconfigurada a PostgreSQL

## 20. CI/CD (Req 12)
- [ ] 20.1 Crear `.github/workflows/ci-cd.yml` con build, tests, publicación de imagen Docker en ECR
- [ ] 20.2 Configurar bloqueo de merge en PR cuando tests o build fallan

## 21. CDK Stack Opcional (Req 12)
- [ ] 21.1 Crear `Monitoreo.Infrastructure/MonitoreoStack.cs` con ECS Fargate, RDS PostgreSQL, ECR, CloudWatch, roles IAM, Security Groups
- [ ] 21.2 Configurar auto-scaling ECS basado en CPU (target 70%, min 1, max 3 tareas)

## 22. Checkpoint - Verificar infraestructura y despliegue
- [ ] 22.1 Ensure all tests pass, ask the user if questions arise.

## 23. Tests Unitarios (Req 13)
- [ ] 23.1 Configurar proyecto de tests con paquetes: xUnit, Moq, FsCheck, FsCheck.Xunit, FluentAssertions
- [ ] 23.2 Crear `MonitoringArbitraries.cs` con generadores FsCheck custom para MonitoringResult y CountryConfig (incluyendo campos de notificación, PFX, QR, CUFE, NucAuthMode)
- [ ] 23.3 Crear tests unitarios para AsmxCertificationService (mock HttpClient, XML, medición tiempo, integración con pipeline)
- [ ] 23.4 Crear tests unitarios para NucCertificationService (mock HttpClient, login + cert, parsing JSON)
  - Incluir tests para modo "static" vs "dynamic" de autenticación
  - Verificar que `BuildNucUsername` interpola correctamente `NucUsernameFormat`
  - Verificar que modo "static" lee token de Secrets Manager sin ejecutar login
- [ ] 23.5 Crear tests unitarios para EmailNotificationService (mock SES client, destinatarios, contenido)
- [ ] 23.6 Crear tests unitarios para WhatsAppNotificationService (mock HttpClient, payload JSON, template)
- [ ] 23.7 Crear tests unitarios para NotificationGateService (kill switch, flags por país/canal, cooldown, logging supresiones)
- [ ] 23.8 Crear tests unitarios para MonitoringOrchestrator (flujo completo con mock de gate, notificaciones condicionales)
- [ ] 23.9 Crear tests unitarios para validación de CountryConfig (campos obligatorios, campos de notificación, validación condicional de PFX ARNs)
- [ ] 23.10 Crear tests unitarios para PfxSigningService
  - Happy path: firma XML válido con PFX válido, verifica presencia de `<Signature>`
  - Error: PFX inválido (base64 corrupto)
  - Error: certificado expirado
  - Error: secreto no encontrado en Secrets Manager
  - _Requerimientos: Req 2 (AC 2.6, 2.9)_
- [ ] 23.11 Crear tests unitarios para QrGenerationService
  - Happy path: genera QR e inyecta en XML
  - Error: XML de entrada inválido
  - _Requerimientos: Req 2 (AC 2.7)_
- [ ] 23.12 Crear tests unitarios para CufeGenerationService
  - Happy path: genera CUFE y obtiene JWT
  - Error: fallo en obtención de JWT (GetJWT)
  - _Requerimientos: Req 2 (AC 2.8)_
- [ ] 23.13 Crear tests unitarios para AsmxPreProcessingPipeline
  - Pipeline completo PA: PFX → QR → CUFE/JWT
  - Pipeline parcial DO: solo PFX
  - Sin pipeline CR/GT/SV: XML sin modificar
  - Fallo en paso del pipeline: retorna error sin continuar
  - _Requerimientos: Req 2 (AC 2.6, 2.7, 2.8)_

## 24. Property-Based Tests (Req 13, Propiedades de Correctitud)
- [ ]* 24.1 [PBT] Propiedad 1: Solo países habilitados se programan — `ConfigurationPropertyTests.cs`
  - **Propiedad 1: Solo países habilitados se programan**
  - **Valida: Requerimientos 1.2**
- [ ]* 24.2 [PBT] Propiedad 2: Cada ciclo produce ambos tipos de certificación — `MonitoringOrchestratorPropertyTests.cs`
  - **Propiedad 2: Cada ciclo produce ambos tipos de certificación**
  - **Valida: Requerimientos 1.4**
- [ ]* 24.3 [PBT] Propiedad 3: Inyección de campos dinámicos en plantillas XML — `CertificationPropertyTests.cs`
  - **Propiedad 3: Inyección de campos dinámicos en plantillas XML**
  - **Valida: Requerimientos 2.1, 3.2**
- [ ]* 24.4 [PBT] Propiedad 4: Invariante de construcción de MonitoringResult — `MonitoringResultPropertyTests.cs`
  - **Propiedad 4: Invariante de construcción de MonitoringResult**
  - **Valida: Requerimientos 2.3, 2.4, 3.3, 3.5, 4.2**
- [ ]* 24.5 [PBT] Propiedad 5: Unicidad de consecutivos atómicos — `CertificationPropertyTests.cs`
  - **Propiedad 5: Unicidad de consecutivos atómicos**
  - **Valida: Requerimientos 2.5**
- [ ]* 24.6 [PBT] Propiedad 6: Round-trip de persistencia en PostgreSQL — `PostgresPropertyTests.cs`
  - **Propiedad 6: Round-trip de persistencia en PostgreSQL**
  - **Valida: Requerimientos 4.1, 13.3**
- [ ]* 24.7 [PBT] Propiedad 7: Lógica de disparo de notificaciones con control manual — `NotificationPropertyTests.cs`
  - **Propiedad 7: Lógica de disparo de notificaciones con control manual**
  - **Valida: Requerimientos 5.1, 5.2, 6.1, 6.2, 7.1, 7.2, 7.3, 7.4**
- [ ]* 24.8 [PBT] Propiedad 8: Completitud y validez del contenido de notificaciones — `NotificationPropertyTests.cs`
  - **Propiedad 8: Completitud y validez del contenido de notificaciones**
  - **Valida: Requerimientos 5.3, 6.3**
- [ ]* 24.9 [PBT] Propiedad 9: Enmascaramiento de credenciales en logs — `SecurityPropertyTests.cs`
  - **Propiedad 9: Enmascaramiento de credenciales en logs**
  - **Valida: Requerimientos 8.3, 8.4**
- [ ]* 24.10 [PBT] Propiedad 10: Validación de CountryConfig — `ConfigurationPropertyTests.cs`
  - **Propiedad 10: Validación de CountryConfig**
  - **Valida: Requerimientos 8.3, 8.4, 7.1, 9.3, 9.4, 9.5, 9.6**
- [ ]* 24.11 [PBT] Propiedad 11: Completitud de logs estructurados — `LoggingPropertyTests.cs`
  - **Propiedad 11: Completitud de logs estructurados**
  - **Valida: Requerimientos 10.3, 10.4**
- [ ]* 24.12 [PBT] Propiedad 12: Correctitud de retención de datos — `RetentionPropertyTests.cs`
  - **Propiedad 12: Correctitud de retención de datos**
  - **Valida: Requerimientos 15.1**
- [ ]* 24.13 [PBT] Propiedad 13: Cooldown de notificaciones previene spam — `NotificationGatePropertyTests.cs`
  - **Propiedad 13: Cooldown de notificaciones previene spam**
  - **Valida: Requerimientos 7.7**
- [ ]* 24.14 [PBT] Propiedad 14: Monitoreo continúa independiente de flags de notificación — `NotificationGatePropertyTests.cs`
  - **Propiedad 14: Monitoreo continúa independiente de flags de notificación**
  - **Valida: Requerimientos 7.2, 7.3**
- [ ]* 24.15 [PBT] Propiedad 15: Firma PFX produce XML firmado válido — `PfxSigningPropertyTests.cs`
  - **Propiedad 15: Firma PFX produce XML firmado válido**
  - **Valida: Requerimientos 2.6, 14.6**
- [ ]* 24.16 [PBT] Propiedad 16: Pipeline de pre-procesamiento aplica pasos correctos según configuración — `AsmxPreProcessingPipelinePropertyTests.cs`
  - **Propiedad 16: Pipeline de pre-procesamiento aplica pasos correctos según configuración**
  - **Valida: Requerimientos 2.6, 2.7, 2.8**
- [ ]* 24.17 [PBT] Propiedad 17: Modo de autenticación NUC determina estrategia de obtención de token — `NucAuthPropertyTests.cs`
  - **Propiedad 17: Modo de autenticación NUC determina estrategia de obtención de token**
  - **Valida: Requerimientos 3.1, 3.2, 9.4, 9.5, 14.7**

## 25. Tests de Integración (Req 13)
- [ ] 25.1 Configurar Testcontainers.PostgreSql para tests de integración
- [ ] 25.2 Crear `PostgresIntegrationTests.cs`: escritura/lectura real, creación de tabla/índices, vista monitoring_summary
- [ ] 25.3 Crear `MonitoringFlowIntegrationTests.cs`: ciclo completo con mocks de endpoints externos y PostgreSQL real

## 26. Checkpoint Final - Verificar todos los tests y wiring
- [ ] 26.1 Ensure all tests pass, ask the user if questions arise.

## Notas

- Las tareas marcadas con `*` son opcionales y pueden omitirse para un MVP más rápido
- Cada tarea referencia requerimientos específicos para trazabilidad
- Los checkpoints aseguran validación incremental
- Los property-based tests validan propiedades universales de correctitud
- Los tests unitarios validan ejemplos específicos y casos borde
- Pipeline de pre-procesamiento por país: GT/SV/CR sin pipeline, DO solo PFX, PA completo (PFX → QR → CUFE/JWT)
- Modos de autenticación NUC: GT usa "static" (token de Secrets Manager), resto usa "dynamic" (login HTTP)
- GT y SV migran de Gmail SMTP a Amazon SES como parte de la unificación
