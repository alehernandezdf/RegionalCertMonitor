---
inclusion: manual
---

# Contexto de Migración - Servicios de Monitoreo Digifact

Este documento contiene el análisis completo de los 5 servicios de monitoreo actuales y sirve como guía para la migración a un servicio unificado.

## Repos Actuales (Org: Digifact-FEL)

| País | Repo | Namespace | Service1.cs Size |
|------|------|-----------|-----------------|
| 🇬🇹 GT | ServicioMonitoreo_GT | mx.com.fact.MonitoreoFEL | ~30KB |
| 🇸🇻 SV | ServicioMonitoreo_SV | sv.com.MonitoreoFE | ~25KB |
| 🇩🇴 DO | ServicioMonitoreo_DO | do.com.MonitoreoFE | ~30KB |
| 🇨🇷 CR | ServicioMonitoreo_CR | cr.com.MonitoreoFE | ~33KB |
| 🇵🇦 PA | ServicioMonitoreo_PA | pa.com.MonitoreoFE | ~64KB |

## Stack Actual

- .NET Framework 4.7.2 (Windows Service legacy)
- System.ServiceProcess (Windows Service)
- System.Data.SqlClient (SQL Server directo)
- HttpWebRequest (HTTP legacy)
- System.Text.Json (parsing manual)
- Web References ASMX (SOAP)
- System.Net.Mail (SMTP via SES)
- WhatsApp Business API (Graph API v17.0)
- Logs a disco local (archivos .txt)
- Config via App.config (appSettings)

## Arquitectura Actual

Cada servicio es un Windows Service que:
1. Se ejecuta con un Timer cada X minutos (configurable via `tick` en App.config)
2. Certifica un documento de prueba via ASMX (SOAP) → mide tiempo de respuesta
3. Certifica un documento de prueba via API NUC (REST) → mide tiempo de respuesta
4. Guarda resultados en SQL Server (stored procedure `InsertResult`)
5. Si hay error o demora > 5 seg → notifica por Email (SES) y WhatsApp
6. Limpia logs viejos los domingos entre 1-2 AM

## Archivos por Repo (estructura típica)

```
{pais}.com.MonitoreoFE/
├── Service1.cs              # GOD CLASS - toda la lógica
├── WhatsappMessages.cs      # Envío de mensajes WhatsApp
├── LogOperations.cs         # Solo en CR y DO
├── DatabaseOperations.cs    # Solo en CR y DO
├── Monitoring.cs            # Modelo simple (TransactionTime, Result, Error, Type)
├── Program.cs               # Entry point del Windows Service
├── ProjectInstaller.cs      # Instalador del servicio
├── App.config               # Configuración con credenciales en texto plano
├── packages.config          # NuGet packages
├── bin/                     # ⚠️ Commiteado al repo
├── obj/                     # ⚠️ Commiteado al repo
└── Web References/          # Referencias SOAP ASMX
```

## Problemas Críticos Identificados

### 🔴 Seguridad
- Connection strings con passwords de RDS en App.config commiteados
- Tokens de WhatsApp Business API en texto plano
- Credenciales SMTP (SES) hardcodeadas
- API tokens y passwords de usuario en el repo
- GT tiene un backup de SQL Server de 44MB (MYSUITEADMIN.bak) en el repo

### 🔴 Arquitectura
- 5 repos con código copy-paste (misma lógica duplicada 5 veces)
- Service1.cs es un God Class (25-64KB) sin separación de responsabilidades
- Sin inyección de dependencias
- Sin patrón de configuración moderno
- HttpWebRequest obsoleto (sin retry, sin circuit breaker, sin timeouts)
- ConfigurationSettings.AppSettings obsoleto desde .NET 2.0
- JSON de WhatsApp construido con concatenación de strings
- Sin manejo de concurrencia en secuenciales (race condition posible)

### 🔴 DevOps
- Sin .gitignore (bin/, obj/, .vs/, packages/ commiteados)
- Sin CI/CD (no hay GitHub Actions ni pipelines)
- Sin tests (cero tests unitarios o de integración)
- Sin containerización
- Deploy manual

### 🟡 Bugs Conocidos
- En CR, SendAlert() tiene el loop de destinatarios duplicado (envía emails dobles)
- GT usa namespace `mx.com.fact` (México?) para un servicio de Guatemala
- Email sender hardcodeado como "MONITOREO FE COSTA RICA" probablemente en todos los países
- PA tiene el Service1.cs más grande (64KB) sugiriendo lógica adicional no estandarizada

## Lo Rescatable

- Concepto de monitoreo por certificación de prueba es correcto
- Medición de tiempo de respuesta con umbral de 5 seg
- Notificación multicanal (Email + WhatsApp)
- CR y DO tienen separación parcial (LogOperations, DatabaseOperations)
- Limpieza automática de logs
- Modelo de datos simple y funcional (Monitoring.cs)

## Configuración por País (extraída de App.config)

### Endpoints ASMX
- GT: https://felaborgt.digifact.com/FEWSFRONT.asmx (estimado)
- SV: endpoint SV ASMX
- DO: endpoint DO ASMX
- CR: https://asmxcr.digifact.com/FEWSFRONT.asmx
- PA: endpoint PA ASMX

### Endpoints API NUC
- CR: https://nuccr.digifact.com/api/cert/xml
- CR Token: https://nuccr.digifact.com/api/login/get_token

### Base de Datos
- SQL Server en RDS (us-west-2): islasrds.ctx2dph65ecb.us-west-2.rds.amazonaws.com
- Database: Monitoring
- Stored Procedure: InsertResult (TransactionTimeStamp, ResultStatus, EventErrorMessage, TIPO)

### Notificaciones
- SMTP: email-smtp.us-east-1.amazonaws.com (SES) puerto 587
- WhatsApp: Graph API v17.0 (template: monitoring_response_mp)

## Modelo de Datos Actual

```csharp
// Monitoring.cs (igual en todos los repos)
public class Monitoring
{
    public decimal TransactionTime { get; set; }
    public bool Result { get; set; }
    public string EventError { get; set; }
    public string Type { get; set; }  // Ej: "ASMX_CR", "NUC_CR"
}
```

## Stored Procedure InsertResult

Parámetros:
- TransactionTimeStamp (Decimal)
- ResultStatus (Bit)
- EventErrorMessage (NVarChar)
- TIPO (NVarChar 50) - formato: {PROTOCOLO}_{PAIS} ej: "ASMX_GT", "NUC_CR"

## Flujo de Certificación ASMX

1. Leer XML nativo de disco (path configurable)
2. Modificar campos dinámicos (Clave, FechaEmision, Consecutivo)
3. Incrementar secuencial y guardarlo en App.config
4. Llamar a `service.RequestTransaction()` via SOAP
5. Medir duración
6. Si duración > 5 seg → alerta WhatsApp
7. Si error → insertar en DB + alerta Email + alerta WhatsApp
8. Si éxito → insertar en DB

## Flujo de Certificación NUC (API REST)

1. Obtener token via POST a /api/login/get_token
2. Leer XML NUC de disco
3. Modificar IssuedDateTime y Consecutivo
4. POST a /api/cert/xml con Authorization header
5. Parsear respuesta JSON (code, message, description, infoDetails)
6. Misma lógica de alertas que ASMX

## Propuesta de Migración (Alto Nivel)

### Target Stack
- .NET 8+ Worker Service (o .NET 9)
- Un solo repo, configuración por país via appsettings.{Country}.json o env vars
- HttpClient + Polly (retry, circuit breaker, timeout)
- Serilog → CloudWatch (o similar)
- AWS Secrets Manager para credenciales
- Docker container
- GitHub Actions CI/CD
- xUnit + tests de integración

### Estructura Propuesta
```
ServicioMonitoreo/
├── src/
│   ├── Monitoreo.Worker/           # Worker Service principal
│   │   ├── Workers/
│   │   │   └── MonitoringWorker.cs
│   │   ├── Services/
│   │   │   ├── ICertificationService.cs
│   │   │   ├── AsmxCertificationService.cs
│   │   │   ├── NucCertificationService.cs
│   │   │   ├── INotificationService.cs
│   │   │   ├── EmailNotificationService.cs
│   │   │   └── WhatsAppNotificationService.cs
│   │   ├── Data/
│   │   │   ├── IMonitoringRepository.cs
│   │   │   └── SqlMonitoringRepository.cs
│   │   ├── Models/
│   │   │   ├── MonitoringResult.cs
│   │   │   └── CountryConfig.cs
│   │   ├── Configuration/
│   │   │   └── MonitoringOptions.cs
│   │   └── Program.cs
│   └── Monitoreo.Shared/           # Modelos y contratos compartidos
├── tests/
│   ├── Monitoreo.Worker.Tests/
│   └── Monitoreo.Integration.Tests/
├── .github/
│   └── workflows/
│       └── ci-cd.yml
├── Dockerfile
├── docker-compose.yml
├── appsettings.json
├── appsettings.GT.json
├── appsettings.SV.json
├── appsettings.DO.json
├── appsettings.CR.json
├── appsettings.PA.json
└── README.md
```
