# Documento de Requerimientos

## Introducción

Migración de los 5 servicios de monitoreo de Digifact (GT, SV, DO, CR, PA) a un servicio unificado en .NET 8+ Worker Service. Actualmente existen 5 repositorios separados con código duplicado (copy-paste), credenciales hardcodeadas, sin tests, sin CI/CD y con una arquitectura de God Class. El objetivo es consolidar todo en un solo servicio configurable por país, con PostgreSQL como base de datos (en Docker para desarrollo, con opción de RDS PostgreSQL en producción), dashboards en la instancia de Grafana existente, servicios nativos de AWS y despliegue rápido mediante Docker Compose con opción de migrar a ECS Fargate.

**Nota sobre variaciones por país:** Cada país tiene particularidades en sus flujos de certificación. PA y DO requieren firma digital PFX, PA adicionalmente requiere generación de QR y CUFE con JWT, y GT usa un token NUC estático en lugar del flujo dinámico de login. Los endpoints NUC varían por país (ej: SV usa `/api/v2/transform/nuc/`, CR usa `/api/cert/xml`). Los proveedores de email actuales difieren (GT/SV usan Gmail SMTP, CR/DO usan SES) pero la migración unifica todo bajo Amazon SES. Estas variaciones se reflejan en los requerimientos a continuación.

## Glosario

- **Worker_Service**: Servicio de background en .NET 8+ que ejecuta tareas periódicas sin interfaz gráfica, desplegado como contenedor Docker (con opción de migrar a ECS Fargate)
- **Monitor**: El servicio unificado de monitoreo que certifica documentos de prueba y reporta resultados
- **Certificación_ASMX**: Proceso de certificar un documento de prueba via protocolo SOAP usando endpoints ASMX de Digifact
- **Certificación_NUC**: Proceso de certificar un documento de prueba via API REST usando endpoints NUC de Digifact
- **País_Config**: Configuración específica por país (GT, SV, DO, CR, PA) que incluye endpoints, credenciales, intervalos, destinatarios de alertas y campos específicos del país (TaxId, Requestor, NucUsername, formato de credenciales, y opcionalmente PfxSecretArn, PfxPasswordSecretArn, QrCode)
- **Resultado_Monitoreo**: Registro que contiene el tiempo de transacción, estado (éxito/fallo), mensaje de error, tipo de certificación y timestamp, almacenado en PostgreSQL_DB
- **Umbral_Alerta**: Tiempo máximo aceptable de respuesta (actualmente 5 segundos en CR/DO, 10 segundos en GT/PA) configurable por país antes de disparar una notificación
- **Notificador**: Componente que envía alertas por Email (Amazon SES) y WhatsApp (Graph API) cuando se detectan errores o demoras
- **PostgreSQL_DB**: Base de datos PostgreSQL que almacena los resultados de monitoreo, ejecutándose en Docker para desarrollo local y con opción de migrar a Amazon RDS PostgreSQL o Aurora PostgreSQL en producción
- **Grafana_Dashboard**: Dashboard de Grafana existente conectado a PostgreSQL_DB como datasource que visualiza métricas de disponibilidad, tiempos de respuesta y alertas por país y tipo de certificación
- **Secrets_Manager**: AWS Secrets Manager para almacenar credenciales de forma segura (connection strings, tokens, API keys, certificados PFX)
- **Circuit_Breaker**: Patrón de resiliencia que detiene temporalmente las llamadas a un servicio externo cuando se detectan fallos consecutivos
- **Health_Check**: Endpoint que reporta el estado de salud del servicio y sus dependencias
- **ECS_Fargate**: AWS ECS con tipo de lanzamiento Fargate como opción de despliegue en producción para ejecutar el Worker_Service como contenedor serverless sin administrar servidores
- **Docker_Compose**: Entorno de desarrollo y despliegue inicial usando Docker Compose con el Worker_Service y PostgreSQL_DB como contenedores, listo para migrar a ECS_Fargate cuando se requiera
- **CloudWatch**: Amazon CloudWatch para logs centralizados, métricas custom y alarmas operativas
- **CDK_Stack**: AWS CDK (Cloud Development Kit) stack en C# que define toda la infraestructura como código para despliegue rápido y repetible
- **ECR**: Amazon Elastic Container Registry para almacenar las imágenes Docker del Worker_Service
- **SSM_Parameter_Store**: AWS Systems Manager Parameter Store para configuración no sensible por ambiente y país
- **Firma_Digital_PFX**: Proceso de firma digital usando certificado PFX (PKCS#12) requerido por PA y DO para firmar documentos antes de la certificación ASMX
- **CUFE**: Código Único de Factura Electrónica, identificador único generado por PA como parte del proceso de certificación
- **Token_Estático_NUC**: Token JWT estático usado por GT para autenticación NUC, almacenado en Secrets_Manager en lugar de obtenerse dinámicamente via login


## Requerimientos

### Requerimiento 1: Ejecución Periódica por País

**User Story:** Como equipo de operaciones de Digifact, quiero que el Monitor ejecute certificaciones de prueba periódicamente para cada país configurado, para detectar caídas o degradación en los servicios de facturación electrónica.

#### Criterios de Aceptación

1. THE Worker_Service SHALL ejecutar ciclos de monitoreo de forma periódica según el intervalo configurado en País_Config para cada país habilitado
2. WHEN el Worker_Service inicia, THE Monitor SHALL cargar la configuración de todos los países habilitados y programar sus ciclos de monitoreo de forma independiente
3. WHILE un ciclo de monitoreo está en ejecución para un país, THE Monitor SHALL permitir que los ciclos de otros países se ejecuten sin bloqueo
4. WHEN el intervalo configurado para un país transcurre, THE Monitor SHALL iniciar un nuevo ciclo de monitoreo para ese país que incluya Certificación_ASMX y Certificación_NUC
5. IF el Worker_Service no puede cargar la configuración de un país al inicio, THEN THE Monitor SHALL registrar el error en los logs y continuar con los demás países

### Requerimiento 2: Certificación ASMX (SOAP)

**User Story:** Como equipo de operaciones, quiero que el Monitor certifique documentos de prueba via SOAP para cada país, para verificar que los endpoints ASMX están operativos.

#### Criterios de Aceptación

1. WHEN un ciclo de monitoreo inicia para un país, THE Monitor SHALL leer la plantilla XML nativa configurada para ese país y modificar los campos dinámicos (Clave, FechaEmision, Consecutivo)
2. WHEN la plantilla XML está preparada, THE Monitor SHALL enviar la solicitud de certificación al endpoint ASMX configurado en País_Config y medir el tiempo de respuesta en milisegundos
3. WHEN la Certificación_ASMX completa exitosamente, THE Monitor SHALL crear un Resultado_Monitoreo con el tiempo de transacción, estado exitoso y tipo "ASMX_{PAIS}"
4. IF la Certificación_ASMX falla por error de red, timeout o respuesta inválida, THEN THE Monitor SHALL crear un Resultado_Monitoreo con estado fallido, el mensaje de error y tipo "ASMX_{PAIS}"
5. THE Monitor SHALL incrementar el consecutivo de certificación ASMX de forma atómica para evitar colisiones entre ciclos concurrentes
6. WHERE País_Config indica que el país requiere Firma_Digital_PFX (PA, DO), THE Monitor SHALL obtener el certificado PFX y su contraseña desde Secrets_Manager (PfxSecretArn, PfxPasswordSecretArn) y firmar digitalmente el documento XML antes de enviarlo al endpoint ASMX
7. WHERE País_Config indica que el país requiere generación de QR (PA), THE Monitor SHALL generar el código QR (ADDQR) e incluirlo en el documento XML antes de la certificación ASMX
8. WHERE País_Config indica que el país requiere CUFE (PA), THE Monitor SHALL generar el Código Único de Factura Electrónica y obtener un JWT (GetJWT) como parte del proceso de preparación del documento antes de la certificación ASMX
9. IF la firma digital PFX falla para un país que la requiere, THEN THE Monitor SHALL crear un Resultado_Monitoreo con estado fallido indicando el error de firma sin intentar la certificación ASMX

### Requerimiento 3: Certificación NUC (API REST)

**User Story:** Como equipo de operaciones, quiero que el Monitor certifique documentos de prueba via API REST NUC para cada país, para verificar que los endpoints REST están operativos.

#### Criterios de Aceptación

1. WHEN un ciclo de monitoreo inicia para un país que tiene flujo dinámico de login NUC (CR, SV, DO, PA), THE Monitor SHALL obtener un token de autenticación del endpoint de login NUC configurado en País_Config usando las credenciales NucUsername y NucPassword del país, donde el formato de username varía por país (ej: CR usa {Country}.{TaxId}.{NucUsername}, SV usa SV.{NRC}.{NIT})
2. WHEN un ciclo de monitoreo inicia para un país que usa Token_Estático_NUC (GT), THE Monitor SHALL obtener el token JWT estático desde Secrets_Manager en lugar de ejecutar el flujo de login dinámico
3. WHEN el token es obtenido (dinámico o estático), THE Monitor SHALL preparar la plantilla XML NUC con los campos dinámicos actualizados (IssuedDateTime, Consecutivo) y enviar la solicitud POST al endpoint de certificación NUC configurado en País_Config (NucCertEndpoint, que varía por país, ej: SV usa /api/v2/transform/nuc/, CR usa /api/cert/xml)
4. WHEN la Certificación_NUC completa exitosamente, THE Monitor SHALL parsear la respuesta JSON (code, message, description, infoDetails) y crear un Resultado_Monitoreo con tipo "NUC_{PAIS}"
5. IF la obtención del token falla (ya sea login dinámico o lectura de token estático), THEN THE Monitor SHALL registrar el error y crear un Resultado_Monitoreo con estado fallido sin intentar la certificación
6. IF la Certificación_NUC falla por error de red, timeout o respuesta inválida, THEN THE Monitor SHALL crear un Resultado_Monitoreo con estado fallido y el mensaje de error


### Requerimiento 4: Persistencia de Resultados en PostgreSQL

**User Story:** Como equipo de operaciones, quiero que todos los resultados de monitoreo se persistan en PostgreSQL, para tener un historial de disponibilidad consultable desde Grafana con la infraestructura de base de datos que ya manejamos.

#### Criterios de Aceptación

1. WHEN un Resultado_Monitoreo es creado, THE Monitor SHALL escribirlo en PostgreSQL_DB en una tabla monitoring_results con las columnas: id (BIGSERIAL PK), country (VARCHAR), certification_type (VARCHAR), endpoint (VARCHAR), transaction_time_ms (DOUBLE PRECISION), result_status (BOOLEAN), event_error_message (TEXT NULL), created_at (TIMESTAMPTZ DEFAULT NOW())
2. THE Monitor SHALL incluir el campo event_error_message en el registro de PostgreSQL_DB cuando el resultado sea fallido, dejándolo NULL cuando sea exitoso
3. THE PostgreSQL_DB SHALL crear índices en las columnas (country, certification_type, created_at) para optimizar las consultas de Grafana por país, tipo y rango temporal
4. IF la escritura en PostgreSQL_DB falla, THEN THE Monitor SHALL registrar el error en los logs y reintentar la escritura según la política de retry configurada sin detener el ciclo de monitoreo
5. THE Monitor SHALL usar Npgsql (driver PostgreSQL para .NET) con connection pooling para optimizar el rendimiento de las escrituras
6. THE Monitor SHALL soportar la connection string de PostgreSQL_DB desde variables de ambiente o Secrets_Manager, permitiendo apuntar a un contenedor Docker local o a RDS PostgreSQL en producción

### Requerimiento 5: Notificaciones por Email (Amazon SES)

**User Story:** Como equipo de operaciones, quiero recibir alertas por email cuando un servicio falle o esté degradado, para poder actuar rápidamente ante incidentes.

#### Criterios de Aceptación

1. WHEN un Resultado_Monitoreo tiene estado fallido AND las notificaciones no están suprimidas para ese país/tipo, THE Notificador SHALL enviar un email de alerta a todos los destinatarios configurados en País_Config para ese país via Amazon SES usando el SDK de AWS (AWSSDK.SimpleEmailV2)
2. WHEN un Resultado_Monitoreo tiene tiempo de transacción mayor al Umbral_Alerta configurado AND las notificaciones no están suprimidas, THE Notificador SHALL enviar un email de alerta indicando degradación del servicio
3. THE Notificador SHALL incluir en el email el país afectado, el tipo de certificación (ASMX o NUC), el tiempo de respuesta, el mensaje de error y la marca de tiempo del evento
4. IF el envío de email via Amazon SES falla, THEN THE Notificador SHALL registrar el error en CloudWatch sin detener el ciclo de monitoreo
5. THE Monitor SHALL migrar los países que actualmente usan Gmail SMTP (GT, SV) a Amazon SES como parte de la unificación, asegurando que todos los países usen un único proveedor de email

### Requerimiento 6: Notificaciones por WhatsApp

**User Story:** Como equipo de operaciones, quiero recibir alertas por WhatsApp cuando un servicio falle o tenga tiempos de respuesta elevados, para tener notificación inmediata en dispositivos móviles.

#### Criterios de Aceptación

1. WHEN un Resultado_Monitoreo tiene estado fallido AND las notificaciones no están suprimidas para ese país/tipo, THE Notificador SHALL enviar un mensaje WhatsApp a los números configurados en País_Config usando la Graph API de WhatsApp con el template monitoring_response_mp
2. WHEN un Resultado_Monitoreo tiene tiempo de transacción mayor al Umbral_Alerta configurado AND las notificaciones no están suprimidas, THE Notificador SHALL enviar un mensaje WhatsApp indicando degradación del servicio
3. THE Notificador SHALL construir el payload JSON de WhatsApp usando serialización estructurada en lugar de concatenación de strings
4. IF el envío de WhatsApp falla, THEN THE Notificador SHALL registrar el error en CloudWatch sin detener el ciclo de monitoreo

### Requerimiento 7: Control Manual de Notificaciones

**User Story:** Como equipo de operaciones, quiero poder activar o desactivar las notificaciones por país y tipo de certificación desde la configuración, para controlar cuándo recibir alertas sin detener el monitoreo.

#### Criterios de Aceptación

1. THE País_Config SHALL incluir parámetros booleanos configurables: notifications_email_enabled y notifications_whatsapp_enabled, que permitan activar o desactivar cada canal de notificación de forma independiente por país
2. WHEN notifications_email_enabled es false para un país, THE Notificador SHALL omitir el envío de emails para ese país pero continuar ejecutando los ciclos de monitoreo y persistiendo resultados en PostgreSQL_DB
3. WHEN notifications_whatsapp_enabled es false para un país, THE Notificador SHALL omitir el envío de WhatsApp para ese país pero continuar ejecutando los ciclos de monitoreo y persistiendo resultados en PostgreSQL_DB
4. THE Monitor SHALL soportar un parámetro global notifications_enabled en SSM_Parameter_Store que actúe como kill switch general para todas las notificaciones de todos los países
5. WHEN se cambia el valor de un parámetro de notificación en SSM_Parameter_Store, THE Monitor SHALL detectar el cambio en el siguiente ciclo de monitoreo sin necesidad de reiniciar el servicio
6. THE Monitor SHALL registrar en los logs cada vez que una notificación es omitida por estar deshabilitada, indicando el país, tipo y canal suprimido
7. THE País_Config SHALL incluir un parámetro configurable notification_cooldown_minutes (por defecto: 15 minutos) que defina un tiempo mínimo entre notificaciones del mismo tipo para el mismo país, evitando spam cuando el servicio está intermitente


### Requerimiento 8: Gestión Segura de Credenciales

**User Story:** Como equipo de seguridad, quiero que todas las credenciales se almacenen de forma segura fuera del código fuente, para eliminar el riesgo de exposición de secretos en el repositorio.

#### Criterios de Aceptación

1. THE Monitor SHALL obtener todas las credenciales sensibles (tokens de WhatsApp, API tokens, passwords de usuario, certificados PFX y sus contraseñas, tokens JWT estáticos) desde Secrets_Manager usando el SDK de AWS (AWSSDK.SecretsManager)
2. THE Monitor SHALL obtener la configuración no sensible (endpoints, intervalos, umbrales, destinatarios) desde SSM_Parameter_Store organizada por jerarquía /monitoreo/{ambiente}/{pais}/
3. THE Monitor SHALL validar que todas las credenciales requeridas estén disponibles al iniciar y registrar un error descriptivo en CloudWatch si alguna falta, sin exponer el valor de la credencial en los logs
4. WHEN el Monitor registra logs o errores, THE Monitor SHALL enmascarar cualquier credencial o dato sensible que pudiera aparecer en mensajes de error de servicios externos
5. THE Worker_Service SHALL excluir del repositorio todos los archivos que contengan credenciales mediante reglas en .gitignore
6. THE Monitor SHALL usar el rol IAM asignado a la tarea ECS_Fargate para autenticarse con los servicios de AWS sin necesidad de access keys estáticas
7. WHERE País_Config indica que el país usa Token_Estático_NUC (GT), THE Monitor SHALL almacenar el token JWT estático en Secrets_Manager migrándolo desde el App.config hardcodeado actual
8. WHERE País_Config indica que el país requiere Firma_Digital_PFX (PA, DO), THE Monitor SHALL almacenar el certificado PFX (base64) y su contraseña en Secrets_Manager usando los ARNs configurados en PfxSecretArn y PfxPasswordSecretArn
9. WHEN el ambiente es Development, THE Monitor SHALL soportar un archivo `appsettings.Secrets.json` como fuente alternativa de credenciales sensibles (tokens, passwords, PFX, connection strings) en lugar de Secrets_Manager, permitiendo desarrollo y pruebas locales sin conexión a AWS
10. THE `appsettings.Secrets.json` SHALL estar incluido en `.gitignore` y THE Worker_Service SHALL incluir un archivo `appsettings.Secrets.template.json` con la estructura esperada (valores vacíos) como referencia para los desarrolladores
11. THE Monitor SHALL cargar credenciales con la siguiente prioridad: (1) Secrets_Manager si está disponible, (2) `appsettings.Secrets.json` si existe y el ambiente es Development, (3) error descriptivo si ninguna fuente está disponible

### Requerimiento 9: Configuración Multi-País

**User Story:** Como equipo de desarrollo, quiero que la configuración de cada país esté separada y sea fácil de mantener, para poder agregar o modificar países sin cambiar código.

#### Criterios de Aceptación

1. THE Monitor SHALL soportar configuración independiente por país mediante SSM_Parameter_Store con jerarquía /monitoreo/{ambiente}/{pais}/ complementada con archivos appsettings.{PAIS}.json para desarrollo local
2. WHEN se agrega un nuevo país, THE Monitor SHALL requerir únicamente la adición de parámetros en SSM_Parameter_Store y secretos en Secrets_Manager sin modificaciones al código fuente
3. THE País_Config SHALL incluir como mínimo los siguientes campos obligatorios: endpoints ASMX y NUC (AsmxEndpoint, NucLoginEndpoint, NucCertEndpoint), intervalo de monitoreo, rutas de plantillas XML, destinatarios de email, números de WhatsApp, umbral de alerta, TaxId (NIT/RUC/Cédula del emisor de prueba), Requestor (GUID del requestor ASMX) y NucUsername (username para login NUC)
4. THE País_Config SHALL incluir un campo nuc_auth_mode (valores: "dynamic" o "static") que indique si el país usa flujo de login dinámico o Token_Estático_NUC
5. THE País_Config SHALL incluir un campo nuc_username_format que defina el formato de credenciales NUC por país (ej: "{Country}.{TaxId}.{NucUsername}" para CR, "SV.{NRC}.{NIT}" para SV) para construir el username de login dinámicamente
6. WHERE País_Config indica que el país requiere Firma_Digital_PFX, THE País_Config SHALL incluir los campos opcionales: PfxSecretArn (ARN del secreto con PFX base64) y PfxPasswordSecretArn (ARN del secreto con la contraseña del PFX)
7. WHERE País_Config indica que el país requiere generación de QR (PA), THE País_Config SHALL incluir el campo opcional QrCode con la configuración necesaria para la generación de códigos QR
8. IF un campo obligatorio de País_Config está ausente o es inválido, THEN THE Monitor SHALL registrar un error descriptivo en CloudWatch al inicio e inhabilitar el monitoreo para ese país
9. THE País_Config SHALL documentar las variaciones conocidas por país: PA requiere Firma_Digital_PFX + QR + CUFE + JWT, DO requiere Firma_Digital_PFX, GT usa Token_Estático_NUC, y los endpoints NUC varían (SV: /api/v2/transform/nuc/, CR: /api/cert/xml)


### Requerimiento 10: Resiliencia y Tolerancia a Fallos

**User Story:** Como equipo de operaciones, quiero que el servicio sea resiliente ante fallos transitorios de red y servicios externos, para evitar alertas falsas y garantizar continuidad.

#### Criterios de Aceptación

1. THE Monitor SHALL aplicar políticas de retry con backoff exponencial en todas las llamadas HTTP a endpoints ASMX, NUC, WhatsApp y Amazon SES
2. THE Monitor SHALL aplicar Circuit_Breaker por endpoint para detener temporalmente las llamadas cuando se detecten fallos consecutivos que superen un umbral configurable
3. THE Monitor SHALL configurar timeouts explícitos en todas las llamadas HTTP, con valores configurables por tipo de operación
4. WHEN un Circuit_Breaker se abre para un endpoint, THE Monitor SHALL registrar el evento en CloudWatch y continuar monitoreando los demás endpoints
5. IF un retry agota todos los intentos configurados, THEN THE Monitor SHALL tratar la operación como fallida y proceder con la lógica de notificación


### Requerimiento 11: Observabilidad y Logging Estructurado

**User Story:** Como equipo de operaciones, quiero logs estructurados y centralizados en CloudWatch para poder diagnosticar problemas rápidamente sin acceder al contenedor.

#### Criterios de Aceptación

1. THE Monitor SHALL generar logs estructurados (JSON) usando Serilog con sink de CloudWatch, incluyendo contexto de país, tipo de certificación, duración y resultado en cada entrada
2. THE Monitor SHALL enviar los logs a un log group de CloudWatch dedicado /ecs/monitoreo-unificado/{ambiente} con retención configurable por ambiente
3. WHEN un ciclo de monitoreo completa, THE Monitor SHALL registrar un log con nivel Information que incluya el país, tipo, duración y resultado
4. WHEN un error ocurre, THE Monitor SHALL registrar un log con nivel Error que incluya el país, tipo, mensaje de error y stack trace
5. THE Monitor SHALL publicar métricas custom en CloudWatch (namespace Digifact/Monitoreo) con dimensiones por país y tipo de certificación, incluyendo: transaction_time_ms, success_count, failure_count y circuit_breaker_state
6. THE Monitor SHALL exponer un Health_Check endpoint en el puerto configurado que reporte el estado del servicio y la conectividad con PostgreSQL_DB y los endpoints de certificación

### Requerimiento 12: Dashboards y Visualización con Grafana

**User Story:** Como equipo de operaciones, quiero dashboards visuales en Grafana conectados a los datos de monitoreo, para tener visibilidad en tiempo real del estado de los servicios de facturación por país.

#### Criterios de Aceptación

1. THE Grafana_Dashboard SHALL conectarse a PostgreSQL_DB como datasource para consultar los resultados de monitoreo usando el datasource nativo de PostgreSQL en Grafana
2. THE Grafana_Dashboard SHALL incluir un panel de disponibilidad por país que muestre el porcentaje de certificaciones exitosas en las últimas 24 horas para cada país y tipo de certificación (ASMX y NUC)
3. THE Grafana_Dashboard SHALL incluir un panel de tiempos de respuesta que muestre gráficas de series de tiempo con los valores de transaction_time_ms agrupados por país y tipo de certificación
4. THE Grafana_Dashboard SHALL incluir un panel de alertas activas que muestre los últimos fallos y degradaciones detectadas con su timestamp, país, tipo y mensaje de error
5. THE Grafana_Dashboard SHALL incluir un panel de tendencias que muestre promedios de tiempo de respuesta por hora y por día para identificar patrones de degradación
6. THE Grafana_Dashboard SHALL incluir variables de filtro por país (GT, SV, DO, CR, PA), tipo de certificación (ASMX, NUC) y rango de tiempo para permitir análisis interactivo
7. THE Worker_Service SHALL incluir un archivo JSON exportable del dashboard de Grafana para facilitar la importación en la instancia existente

### Requerimiento 13: Despliegue con Docker Compose y Opción de Migración a ECS Fargate

**User Story:** Como equipo de DevOps, quiero que el servicio se despliegue rápidamente con Docker Compose incluyendo PostgreSQL, y que esté preparado para migrar a ECS Fargate cuando se requiera escalar en producción.

#### Criterios de Aceptación

1. THE Worker_Service SHALL incluir un archivo docker-compose.yml que levante el servicio completo con: Worker_Service, PostgreSQL_DB y opcionalmente Grafana, permitiendo ejecutar todo el stack con un solo comando docker compose up
2. THE Worker_Service SHALL incluir un Dockerfile multi-stage que produzca una imagen optimizada basada en la imagen oficial de .NET 8+ runtime con tamaño menor a 200MB
3. THE docker-compose.yml SHALL configurar PostgreSQL_DB con volumen persistente, health check, y script de inicialización que cree la tabla monitoring_results y sus índices
4. THE Worker_Service SHALL incluir un pipeline de CI/CD en GitHub Actions que ejecute build, tests y publicación de imagen Docker en ECR
5. WHEN el pipeline de CI detecta un fallo en tests o build, THE Worker_Service SHALL bloquear el merge del pull request
6. THE Worker_Service SHALL usar variables de ambiente para toda la configuración, permitiendo cambiar entre Docker Compose local y ECS Fargate sin modificar código
7. THE CDK_Stack SHALL estar disponible como opción para despliegue en producción, definiendo en C# usando AWS CDK: ECS_Fargate cluster y servicio, RDS PostgreSQL, ECR repositorio, CloudWatch log groups, roles IAM y Security Groups
8. THE CDK_Stack SHALL crear un ECS_Fargate service con auto-scaling basado en CPU (target 70%) y un mínimo de 1 tarea y máximo de 3 tareas


### Requerimiento 14: Testing

**User Story:** Como equipo de desarrollo, quiero que el servicio tenga cobertura de tests automatizados, para garantizar la calidad del código y prevenir regresiones.

#### Criterios de Aceptación

1. THE Worker_Service SHALL incluir tests unitarios con xUnit para los servicios de certificación, notificación y persistencia usando mocks de las dependencias externas (PostgreSQL, SES, WhatsApp)
2. THE Worker_Service SHALL incluir tests de integración que validen la conectividad con PostgreSQL_DB y el flujo completo de un ciclo de monitoreo usando el contenedor PostgreSQL de Docker Compose
3. WHEN se ejecutan los tests unitarios, THE Worker_Service SHALL validar que un Resultado_Monitoreo creado con datos válidos y luego serializado y deserializado produce un objeto equivalente al original (propiedad round-trip)
4. THE Worker_Service SHALL incluir tests que validen que la configuración de cada país se carga correctamente y que los campos obligatorios son validados
5. THE Worker_Service SHALL incluir tests que validen la escritura y lectura de registros en PostgreSQL_DB verificando que las columnas se persisten correctamente
6. THE Worker_Service SHALL incluir tests que validen el flujo de Certificación_ASMX con Firma_Digital_PFX para países que lo requieren (PA, DO), verificando que el documento se firma correctamente antes del envío
7. THE Worker_Service SHALL incluir tests que validen ambos modos de autenticación NUC: flujo dinámico de login (CR, SV, DO, PA) y Token_Estático_NUC (GT)

### Requerimiento 15: Herramienta Visual para Pruebas Locales

**User Story:** Como desarrollador, quiero poder visualizar los resultados de monitoreo directamente desde PostgreSQL durante el desarrollo local, para verificar tiempos de respuesta de cada certificación sin depender de Grafana.

#### Criterios de Aceptación

1. THE docker-compose.yml SHALL incluir un servicio de pgAdmin como herramienta de administración visual de PostgreSQL_DB, accesible en el puerto 5050 del host local
2. THE docker-compose.yml SHALL preconfigurar pgAdmin con la conexión a PostgreSQL_DB para que al abrir el navegador ya esté conectado sin configuración manual
3. THE Worker_Service SHALL incluir un archivo SQL con queries predefinidas para pruebas visuales que incluyan: últimos resultados por país, tiempos de respuesta promedio por tipo de certificación, y fallos recientes
4. THE PostgreSQL_DB SHALL incluir una vista (VIEW) monitoring_summary que agregue los resultados por país y tipo de certificación mostrando: último tiempo de respuesta, promedio últimas 24h, cantidad de éxitos, cantidad de fallos y último error

### Requerimiento 16: Retención y Limpieza Automática de Datos

**User Story:** Como equipo de operaciones, quiero que los datos históricos de monitoreo se gestionen automáticamente, para optimizar costos de almacenamiento sin perder visibilidad operativa.

#### Criterios de Aceptación

1. THE PostgreSQL_DB SHALL gestionar la retención de datos mediante una tarea programada que elimine registros de monitoring_results con created_at mayor a 365 días
2. THE Monitor SHALL ejecutar la tarea de limpieza de datos antiguos en PostgreSQL_DB de forma periódica según un schedule configurable (por defecto: diariamente a las 2 AM)
3. THE CloudWatch log group SHALL configurar retención de logs a 90 días para optimizar costos de almacenamiento
4. IF la tarea de limpieza de datos falla, THEN THE Monitor SHALL registrar el error en los logs sin afectar los ciclos de monitoreo activos