-- BEGIN-FEAT::BE-664::2026-03-17::AHL::Script de inicialización PostgreSQL con tabla, índices y vista de resumen
CREATE TABLE IF NOT EXISTS monitoring_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    country VARCHAR(5) NOT NULL,
    certification_type VARCHAR(10) NOT NULL,
    endpoint TEXT NOT NULL,
    transaction_time_ms BIGINT NOT NULL,
    result_status BOOLEAN NOT NULL,
    event_error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_monitoring_results_country
    ON monitoring_results (country);

CREATE INDEX IF NOT EXISTS idx_monitoring_results_created_at
    ON monitoring_results (created_at DESC);

CREATE INDEX IF NOT EXISTS idx_monitoring_results_country_type_created
    ON monitoring_results (country, certification_type, created_at DESC);

CREATE OR REPLACE VIEW monitoring_summary AS
SELECT
    country,
    certification_type,
    COUNT(*) AS total_checks,
    SUM(CASE WHEN result_status THEN 1 ELSE 0 END) AS success_count,
    SUM(CASE WHEN NOT result_status THEN 1 ELSE 0 END) AS failure_count,
    ROUND(AVG(transaction_time_ms)::numeric, 2) AS avg_time_ms,
    MAX(transaction_time_ms) AS max_time_ms,
    MIN(transaction_time_ms) AS min_time_ms,
    MAX(created_at) AS last_check
FROM monitoring_results
WHERE created_at >= NOW() - INTERVAL '24 hours'
GROUP BY country, certification_type
ORDER BY country, certification_type;
-- END-FEAT::BE-664::2026-03-17::AHL::Script de inicialización PostgreSQL con tabla, índices y vista de resumen

-- BEGIN-FEAT::BE-660::2026-03-26::AHL::Tabla de consecutivos por país y tipo para control de secuencial entre reinicios
CREATE TABLE IF NOT EXISTS sequential_counters (
    country VARCHAR(5) NOT NULL,
    cert_type VARCHAR(10) NOT NULL,
    last_value BIGINT NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (country, cert_type)
);

-- Inicializar consecutivos para cada país/tipo
INSERT INTO sequential_counters (country, cert_type, last_value) VALUES
    ('CR', 'ASMX', 0), ('CR', 'NUC', 0),
    ('GT', 'ASMX', 0), ('GT', 'NUC', 0),
    ('SV', 'ASMX', 0), ('SV', 'NUC', 0),
    ('DO', 'ASMX', 0), ('DO', 'NUC', 0),
    ('PA', 'ASMX', 0), ('PA', 'NUC', 0)
ON CONFLICT (country, cert_type) DO NOTHING;
-- END-FEAT::BE-660::2026-03-26::AHL::Tabla de consecutivos por país y tipo para control de secuencial entre reinicios

-- BEGIN-FEAT::BE-672::2026-07-01::AHL::Tabla de incidentes por país (contador "días sin incidentes", registro MANUAL)
-- Para registrar un incidente nuevo (reinicia el contador de ese país):
--   INSERT INTO incidents (country, incident_date, note) VALUES ('GT', '2026-07-15', 'Descripcion del incidente');
CREATE TABLE IF NOT EXISTS incidents (
    id SERIAL PRIMARY KEY,
    country VARCHAR(5) NOT NULL,
    incident_date DATE NOT NULL,
    note TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_incidents_country_date
    ON incidents (country, incident_date DESC);

-- Semilla: últimos incidentes conocidos (contadores del pizarrón al viernes 26/06/2026)
INSERT INTO incidents (country, incident_date, note)
SELECT v.country, v.incident_date::date, v.note
FROM (VALUES
    ('GT', '2026-05-08', 'Semilla inicial (49 dias al 26/06/2026)'),
    ('SV', '2026-06-20', 'Semilla inicial (6 dias al 26/06/2026)'),
    ('CR', '2025-10-20', 'Semilla inicial (249 dias al 26/06/2026)'),
    ('PA', '2025-11-26', 'Semilla inicial (212 dias al 26/06/2026)'),
    ('DO', '2025-07-19', 'Semilla inicial (342 dias al 26/06/2026)')
) AS v(country, incident_date, note)
WHERE NOT EXISTS (SELECT 1 FROM incidents i WHERE i.country = v.country);
-- END-FEAT::BE-672::2026-07-01::AHL::Tabla de incidentes por país
