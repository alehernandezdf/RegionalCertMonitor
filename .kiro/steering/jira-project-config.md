---
inclusion: auto
---

# Configuración de Proyecto Jira - Digifact

Este steering define las reglas obligatorias para la creación y gestión de tickets en Jira para los equipos de Digifact.

## Proyectos Jira Disponibles

| Proyecto | Key | Descripción |
|----------|-----|-------------|
| APPMOVIL | APM | Aplicación móvil |
| BACKEND | BE | Servicios backend |
| DEVOPS | DPS | Infraestructura y DevOps |
| Digifact Request & Task Incidents | DRTI | Incidentes y solicitudes |
| FRONTEND | FE | Interfaces de usuario |
| INFRAWS | IWS | Infraestructura web services |
| INTEGRACIONES | ITG | Integraciones con terceros |
| MEJORA CONTINUA | MC | Mejora continua |
| PERIFERICOS | PER | Periféricos |

**Regla:** Si el usuario no especifica el proyecto, preguntar antes de crear tickets.

## Campos Obligatorios en Cada Issue

Todos los tickets (Epic, Story, Task, Sub-task, Bug) deben incluir:

| Campo | Regla | Campo Jira |
|-------|-------|------------|
| Proyecto | Preguntar si no se especifica | project.key |
| Assignee | Preguntar si no se especifica | accountId del usuario |
| País | Preguntar si no se especifica | customfield_10106 |
| Componente | Depende del proyecto, preguntar si hay duda | components |

### Opciones de País

| País | ID | Código Summary |
|------|-----|----------------|
| Guatemala | 10113 | GT |
| Panamá | 10114 | PA |
| El Salvador | 10115 | SV |
| República Dominicana | 10116 | RD |
| Costa Rica | 10117 | CR |
| Ninguno | 10214 | — |

### Regla Multi-País (CRÍTICA)

Cuando un requerimiento o incidente aplica a **varios países**:
- El campo País (`customfield_10106`) DEBE incluir **TODOS** los países que aplican como array
- El summary usa `[MULTI]` como código de país
- **NUNCA** usar "Ninguno" (10214) cuando hay países involucrados
- "Ninguno" (10214) SOLO se usa cuando el ticket genuinamente no tiene país asociado (ej: tarea interna de infraestructura sin relación a país)

Ejemplo multi-país (5 países):
```json
"customfield_10106": [
  {"id": "10113"},
  {"id": "10114"},
  {"id": "10115"},
  {"id": "10116"},
  {"id": "10117"}
]
```

Ejemplo un solo país (El Salvador):
```json
"customfield_10106": [{"id": "10115"}]
```

### Componentes del Proyecto BACKEND (BE)

| Componente | ID |
|------------|-----|
| Administrativo | 10131 |
| CONSTRUCTS | 10329 |
| CORE BACKEND | 10099 |
| NUC | 10098 |

**Nota:** Otros proyectos pueden tener componentes diferentes. Consultar via API si el proyecto no es BE.

## Tipos de Issue

| Tipo | ID | Uso |
|------|-----|-----|
| Epic | 10000 | Agrupación de funcionalidad completa |
| Story | 10007 | Requerimiento individual (1 Story = 1 Requerimiento del requirements.md) |
| Task | 10044 | Tarea técnica independiente |
| Sub-task | 10045 | Subtarea dentro de una Story |
| Bug | 10046 | Corrección de defectos |

## Estructura de Tickets para Specs

Cuando se crea una spec con requirements.md, la estructura en Jira debe ser:

1. **1 Epic** por spec/feature
   - Nombre: Descripción corta de la funcionalidad
   - Descripción: Resumen del alcance + referencia a la spec en el repo

2. **1 Story por cada Requerimiento** del requirements.md
   - Nombre: Título del requerimiento
   - Descripción: User story + criterios de aceptación + referencia a tareas en tasks.md
   - Cada Story mapea 1:1 con un requerimiento. Kiro ejecuta la implementación, el dev valida y testea

3. **Subtareas técnicas** (opcional, dentro de cada Story):
   - Implementación
   - Validaciones
   - Testing
   - Logs

## Reglas de Creación

- Preguntar el proyecto solo si no se especifica en la solicitud
- Siempre preguntar el país si no se especifica
- Siempre preguntar el componente si el proyecto no es BE o si hay duda
- Siempre preguntar el assignee si no se especifica. Buscar via API `/rest/api/3/user/search` si no está en la tabla de usuarios conocidos
- No afectar lógica existente de otros países
- Cada Story debe referenciar el requerimiento correspondiente del requirements.md
- La descripción del Epic debe incluir la ruta de la spec en el repo

## Usuarios Conocidos

| Nombre | accountId |
|--------|-----------|
| Alejandro Hernández | 712020:66c921bb-37da-46ab-9343-0b96bcce5f1b |

**Nota:** Para otros devs, buscar via API `/rest/api/3/user/search`.

---

## Proyecto DRTI — Reportar Incidente o Requerimiento

El proyecto DRTI usa Jira Service Management. Los tickets se crean como Service Requests.

### Issue Type

| Tipo | ID | Nombre interno |
|------|-----|----------------|
| Service Request | 10058 | [System] Service request |

**Request Type ID:** 99 (Reportar Incidente o Requerimiento)

### Campos del Formulario DRTI

| Campo | Campo Jira | Obligatorio | Regla |
|-------|------------|-------------|-------|
| Resumen | summary | Sí | Formato: `[CÓDIGO_PAÍS] Tipo - Descripción corta` |
| Producto | components | Sí | Preguntar si no se especifica |
| Tipo Reporte | customfield_10137 | Sí | Preguntar siempre (Requerimiento o Incidente) |
| País | customfield_10106 | Sí | Preguntar si no se especifica. Formato array: `[{"id": "ID"}]`. Si multi-país, incluir TODOS los IDs |
| Ambiente afectado | customfield_10170 | Sí | Preguntar siempre |
| Urgencia | customfield_10048 | Sí | Preguntar si no se especifica |
| Impacto | customfield_10004 | Sí | Preguntar si no se especifica |
| Descripción | description | Sí | Usar plantilla ADF (ver ejemplo completo abajo) |
| Reporter | reporter | Sí | Preguntar siempre. Usar `{"accountId": "..."}`. Por defecto sugerir al lead (Alejandro Hernández) |

### Opciones de Tipo Reporte (customfield_10137)

| Opción | ID |
|--------|-----|
| Requerimiento | 10145 |
| Incidente | 10146 |

### Opciones de Ambiente Afectado (customfield_10170)

| Opción | ID |
|--------|-----|
| Producción | 10177 |
| Certificación | 10178 |
| Ambos | 10179 |

### Opciones de Urgencia (customfield_10048)

| Opción | ID |
|--------|-----|
| Low | 10025 |
| Medium | 10026 |
| High | 10024 |
| Critical | 10027 |

### Opciones de Impacto (customfield_10004)

| Opción | ID |
|--------|-----|
| Minor/Localized | 10001 |
| Moderate/Limited | 10002 |
| Significant/Large | 10003 |
| Extensive/Widespread | 10000 |

### Componentes del Proyecto DRTI

| Componente | ID |
|------------|-----|
| APPMOVIL | 10300 |
| BACKEND | 10301 |
| CONSTRUCTS | 10330 |
| DEVOPS | 10302 |
| FRONTEND | 10303 |
| INFRAWS | 10304 |
| INTEGRACIONES | 10305 |
| NUC | 10306 |
| PERIFERICOS | 10307 |
| Administrativo | 10308 |
| Contabilidad | 10309 |
| Comercial | 10310 |
| Soporte | 10311 |
| Operaciones | 10312 |
| Recursos Humanos | 10313 |
| Legal | 10314 |
| Gerencia | 10315 |
| Marketing | 10316 |
| Calidad | 10317 |
| Seguridad | 10318 |

### Formato de Summary DRTI

```
[CÓDIGO_PAÍS] Tipo - Descripción corta
```

Códigos de país: GT, PA, SV, RD, CR, MULTI (varios países)

Ejemplos:
- `[SV] Requerimiento - Generación de Libros de Ventas F-07 IVA`
- `[GT] Incidente - Error en generación de PDF Anexo 1`
- `[MULTI] Requerimiento - Servicio Unificado de Monitoreo .NET 8+`

### Plantilla de Descripción DRTI — Formato ADF (OBLIGATORIO)

La descripción DEBE usar Atlassian Document Format (ADF). **NO usar texto plano con hardBreaks.**

Estructura ADF requerida con secciones bold, bullet lists y párrafos separados:

```json
{
  "type": "doc",
  "version": 1,
  "content": [
    {
      "type": "paragraph",
      "content": [
        {"type": "text", "text": "📋 Descripción", "marks": [{"type": "strong"}]}
      ]
    },
    {
      "type": "paragraph",
      "content": [
        {"type": "text", "text": "[Descripción clara del requerimiento o incidente]"}
      ]
    },
    {
      "type": "paragraph",
      "content": [
        {"type": "text", "text": "🎯 Alcance", "marks": [{"type": "strong"}]}
      ]
    },
    {
      "type": "bulletList",
      "content": [
        {
          "type": "listItem",
          "content": [
            {"type": "paragraph", "content": [{"type": "text", "text": "[Punto 1]"}]}
          ]
        },
        {
          "type": "listItem",
          "content": [
            {"type": "paragraph", "content": [{"type": "text", "text": "[Punto 2]"}]}
          ]
        }
      ]
    },
    {
      "type": "paragraph",
      "content": [
        {"type": "text", "text": "🔗 Referencia Técnica", "marks": [{"type": "strong"}]}
      ]
    },
    {
      "type": "bulletList",
      "content": [
        {
          "type": "listItem",
          "content": [
            {"type": "paragraph", "content": [{"type": "text", "text": "Epic: [KEY del Epic relacionado]"}]}
          ]
        },
        {
          "type": "listItem",
          "content": [
            {"type": "paragraph", "content": [{"type": "text", "text": "Spec: [Ruta de la spec en el repo, si aplica]"}]}
          ]
        }
      ]
    },
    {
      "type": "paragraph",
      "content": [
        {"type": "text", "text": "📎 Adjuntar archivos de referencia manualmente (la API no soporta multipart)", "marks": [{"type": "em"}]}
      ]
    }
  ]
}
```

**Nota:** No incluir solicitante ni fecha en la descripción. El reporter se asigna como campo del ticket y la fecha es automática.

### Ejemplo Completo — Payload POST para crear ticket DRTI

Endpoint: `POST /rest/api/3/issue`

```json
{
  "fields": {
    "project": {"key": "DRTI"},
    "issuetype": {"id": "10058"},
    "summary": "[SV] Requerimiento - Generación de Libros de Ventas F-07 IVA",
    "reporter": {"accountId": "712020:66c921bb-37da-46ab-9343-0b96bcce5f1b"},
    "components": [{"id": "10301"}],
    "customfield_10137": {"id": "10145"},
    "customfield_10106": [{"id": "10115"}],
    "customfield_10170": {"id": "10179"},
    "customfield_10048": {"id": "10024"},
    "customfield_10004": {"id": "10000"},
    "description": {
      "type": "doc",
      "version": 1,
      "content": [
        {
          "type": "paragraph",
          "content": [
            {"type": "text", "text": "📋 Descripción", "marks": [{"type": "strong"}]}
          ]
        },
        {
          "type": "paragraph",
          "content": [
            {"type": "text", "text": "Implementar la generación de los Libros de Ventas del formulario F-07 de IVA para El Salvador."}
          ]
        },
        {
          "type": "paragraph",
          "content": [
            {"type": "text", "text": "🎯 Alcance", "marks": [{"type": "strong"}]}
          ]
        },
        {
          "type": "bulletList",
          "content": [
            {
              "type": "listItem",
              "content": [
                {"type": "paragraph", "content": [{"type": "text", "text": "Libro de Ventas a Contribuyentes"}]}
              ]
            },
            {
              "type": "listItem",
              "content": [
                {"type": "paragraph", "content": [{"type": "text", "text": "Libro de Ventas a Consumidores"}]}
              ]
            },
            {
              "type": "listItem",
              "content": [
                {"type": "paragraph", "content": [{"type": "text", "text": "Cálculos fiscales IVA 13%"}]}
              ]
            }
          ]
        },
        {
          "type": "paragraph",
          "content": [
            {"type": "text", "text": "🔗 Referencia Técnica", "marks": [{"type": "strong"}]}
          ]
        },
        {
          "type": "bulletList",
          "content": [
            {
              "type": "listItem",
              "content": [
                {"type": "paragraph", "content": [{"type": "text", "text": "Epic BE: BE-650"}]}
              ]
            },
            {
              "type": "listItem",
              "content": [
                {"type": "paragraph", "content": [{"type": "text", "text": "Spec: .kiro/specs/f07-libros-ventas-sv/"}]}
              ]
            }
          ]
        },
        {
          "type": "paragraph",
          "content": [
            {"type": "text", "text": "📎 Adjuntar archivos de referencia manualmente", "marks": [{"type": "em"}]}
          ]
        }
      ]
    }
  }
}
```

### Reglas de Creación DRTI

- Siempre preguntar: Tipo Reporte, Ambiente afectado, País, Producto
- Siempre preguntar el Reporter. Sugerir al lead (Alejandro Hernández) por defecto
- Preguntar Urgencia e Impacto si no se especifican
- Si hay una Épica relacionada en otro proyecto (ej. BE), linkear el ticket DRTI a la Épica usando:
  - POST `/rest/api/3/issueLink` con `{"inwardIssue": {"key": "DRTI-XXXX"}, "outwardIssue": {"key": "BE-XXXX"}, "type": {"name": "Relates"}}`
- Los adjuntos no se pueden subir via API (requiere multipart). Mencionar en la descripción que se deben adjuntar manualmente
- El campo País (customfield_10106) requiere formato array: `[{"id": "ID"}]`
- Si es multi-país, incluir TODOS los países que aplican en el array (ver Regla Multi-País arriba)
- La descripción DEBE usar formato ADF con párrafos, bulletList y marks (ver ejemplo completo arriba). NO usar texto plano con hardBreaks
