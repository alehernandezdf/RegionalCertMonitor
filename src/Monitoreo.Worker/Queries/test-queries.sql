-- DOCS::BE-660::2026-03-17::AHL::Queries predefinidas para verificación y diagnóstico del monitoreo

-- 1. Últimos 20 resultados por país
SELECT id, country, certification_type, endpoint, transaction_time_ms,
       result_status, event_error_message, created_at
FROM monitoring_results
ORDER BY created_at DESC
LIMIT 20;

-- 2. Últimos resultados por país (más reciente de cada uno)
SELECT DISTINCT ON (country, certification_type)
       country, certification_type, transaction_time_ms,
       result_status, event_error_message, created_at
FROM monitoring_results
ORDER BY country, certification_type, created_at DESC;

-- 3. Promedios de tiempo de respuesta por tipo (última hora)
SELECT country, certification_type,
       ROUND(AVG(transaction_time_ms), 2) AS avg_ms,
       MIN(transaction_time_ms) AS min_ms,
       MAX(transaction_time_ms) AS max_ms,
       COUNT(*) AS total
FROM monitoring_results
WHERE created_at >= NOW() - INTERVAL '1 hour'
GROUP BY country, certification_type
ORDER BY country, certification_type;

-- 4. Fallos recientes (últimas 2 horas)
SELECT country, certification_type, endpoint,
       transaction_time_ms, event_error_message, created_at
FROM monitoring_results
WHERE result_status = false
  AND created_at >= NOW() - INTERVAL '2 hours'
ORDER BY created_at DESC;

-- 5. Disponibilidad por país (últimas 24 horas)
SELECT country, certification_type,
       COUNT(*) AS total,
       SUM(CASE WHEN result_status THEN 1 ELSE 0 END) AS exitosos,
       SUM(CASE WHEN NOT result_status THEN 1 ELSE 0 END) AS fallidos,
       ROUND(100.0 * SUM(CASE WHEN result_status THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0), 2) AS disponibilidad_pct
FROM monitoring_results
WHERE created_at >= NOW() - INTERVAL '24 hours'
GROUP BY country, certification_type
ORDER BY country, certification_type;

-- 6. Vista resumen (usa la vista creada en init.sql)
SELECT * FROM monitoring_summary;

-- 7. Conteo de registros por día (últimos 7 días)
SELECT DATE(created_at) AS fecha, country, COUNT(*) AS total
FROM monitoring_results
WHERE created_at >= NOW() - INTERVAL '7 days'
GROUP BY DATE(created_at), country
ORDER BY fecha DESC, country;
