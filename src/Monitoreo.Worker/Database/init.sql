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

-- BEGIN-FEAT::BE-672::2026-07-20::AHL::Destinatarios de notificaciones en BD: agregar/quitar correos y numeros SIN deploy
-- country '*' aplica a todos los paises; channel es 'email' o 'whatsapp'.
-- Agregar:    INSERT INTO notification_recipients (country, channel, destination) VALUES ('*', 'email', 'nuevo@digifact.com');
-- Desactivar: UPDATE notification_recipients SET enabled = false WHERE destination = 'fulano@digifact.com';
CREATE TABLE IF NOT EXISTS notification_recipients (
    id SERIAL PRIMARY KEY,
    country VARCHAR(5) NOT NULL DEFAULT '*',
    channel VARCHAR(10) NOT NULL,
    destination TEXT NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (country, channel, destination)
);

INSERT INTO notification_recipients (country, channel, destination) VALUES
    ('*', 'email', 'alejandro.hernandez@digifact.com'),
    ('*', 'email', 'juan.giammattei@digifact.com'),
    ('*', 'email', 'joshua.equite@digifact.com'),
    ('*', 'email', 'julio.cifuentes@digifact.com'),
    ('*', 'email', 'daniel.jimenez@digifact.com'),
    ('*', 'email', 'ramiro.morales@digifact.com'),
    ('*', 'email', 'diego.bercian@digifact.com'),
    ('*', 'email', 'hector.lau@digifact.com'),
    ('*', 'email', 'pablo.culajay@digifact.com'),
    ('*', 'email', 'raynert.pantoja@digifact.com'),
    ('*', 'whatsapp', '50230002383'),
    ('*', 'whatsapp', '50232747582'),
    ('*', 'whatsapp', '50240209249'),
    ('*', 'whatsapp', '50253276129'),
    ('*', 'whatsapp', '50249099817'),
    ('*', 'whatsapp', '50250533652'),
    ('*', 'whatsapp', '50233487682'),
    ('*', 'whatsapp', '50256320736'),
    ('*', 'whatsapp', '50763884110')
ON CONFLICT (country, channel, destination) DO NOTHING;
-- END-FEAT::BE-672::2026-07-20::AHL::Destinatarios de notificaciones en BD

-- BEGIN-FEAT::BE-672::2026-07-20::AHL::Cola de alertas de PRUEBA manuales
-- Disparar prueba: INSERT INTO alert_test_queue (channel) VALUES ('email');  -- 'email' | 'whatsapp' | 'all'
-- El worker la consume en <=15s y envia una alerta de PRUEBA a los destinatarios activos de notification_recipients.
CREATE TABLE IF NOT EXISTS alert_test_queue (
    id SERIAL PRIMARY KEY,
    channel VARCHAR(10) NOT NULL DEFAULT 'email',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
-- END-FEAT::BE-672::2026-07-20::AHL::Cola de alertas de PRUEBA manuales
