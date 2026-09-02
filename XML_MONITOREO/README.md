# ⚠️ XML de REFERENCIA — el worker NO usa estos archivos

Los XML de esta carpeta son **copias históricas del monitoreo viejo** (los Windows Services
por país). Se trajeron al repo como material de referencia cuando se construyó el worker
unificado, y **ningún componente los lee**.

**Editar estos XML no cambia nada en el monitoreo.**

## Los templates REALES están en:

```
src/Monitoreo.Worker/Templates/{GT|SV|CR|DO|PA}/asmx-template.xml
src/Monitoreo.Worker/Templates/{GT|SV|CR|DO|PA}/nuc-template.xml
```

(configurados por país en `src/Monitoreo.Worker/appsettings.{PAIS}.json` →
`AsmxTemplatePath` / `NucTemplatePath`)

## Lo único vivo de esta carpeta

`XML_MONITOREO/Resources/` — las banderas PNG que Grafana monta para los dashboards
(ver `docker-compose.yml`). No borrar esa subcarpeta.

---
*Contexto: en DRTI-6905 se editó por error el XML de CR de esta carpeta y el fix no surtió
efecto. Este README existe para que no vuelva a pasar.*
