# Mapas de sucursales

Las coordenadas siguen siendo Center.Latitude/Longitude. Google solo renderiza los mapas.
No se necesitan migraciones, Places, Geocoding, Routes ni Street View.
La búsqueda Nominatim ya no aparece en el formulario; su servicio existente no se modifica.

## Configuración

- Docker Compose: configurar VITE_GOOGLE_MAPS_API_KEY en el .env local ignorado y recrear el servicio web.
- Vite local: exportar VITE_GOOGLE_MAPS_API_KEY en el entorno o usar apps/web/.env.local (ignorado).
- Build de producción: inyectar estas variables en el entorno de compilación y reconstruir el frontend.
- VITE_GOOGLE_MAPS_MAP_ID: ID de mapa JavaScript propio para los marcadores avanzados.
  Si falta se usa DEMO_MAP_ID, únicamente apropiado para pruebas. Usar un ID propio en producción.
- No incluir claves reales en Git. Las variables VITE son visibles en el navegador:
  su protección es la restricción en Google Cloud, no ocultarlas como secreto del servidor.

## Google Cloud

Habilitar únicamente Maps JavaScript API y configurar la facturación requerida.
Restringir la clave por sitios web/HTTP referrers:
http://localhost:5173/* y http://127.0.0.1:5173/* solo para desarrollo.
En producción autorizar exclusivamente https://DOMINIO-REAL/* y los subdominios realmente utilizados.
Preferir claves separadas por entorno. No usar comodines que permitan cualquier dominio.
Restringir APIs a Maps JavaScript API. Configurar cuotas y alertas de presupuesto.
Estas restricciones requieren acceso a Google Cloud y no se aplican mediante este repositorio.

## Comportamiento

Un cargador compartido inserta un solo script; importa únicamente la biblioteca marker junto al mapa.
Un mapa público contiene todos los marcadores y ajusta LatLngBounds. Street View está deshabilitado.
Sin clave, error de red, timeout o error de autorización se muestra una alternativa sin bloquear
las coordenadas manuales ni las tarjetas y enlaces externos.
Los enlaces de indicaciones usan coordenadas, sin llamar a Routes API.

## Referencias oficiales

- https://developers.google.com/maps/documentation/javascript/load-maps-js-api
- https://developers.google.com/maps/documentation/javascript/advanced-markers/start
- https://developers.google.com/maps/api-security-best-practices
