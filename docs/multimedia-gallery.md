# Galería multimedia

## Modelo y conservación
ClinicMediaItems pertenece a Clinics (RESTRICT). Orden entero y tipo enum convertido a texto; índice (ClinicId, IsActive, SortOrder). La referencia única opcional LegacyImageId tiene FK RESTRICT a ClinicSiteImages. La migración AddClinicMultimediaGallery incorpora las portadas HERO existentes como elementos IMAGE, sin copiar bytes ni modificar CARE. Eliminar ese elemento elimina su publicación, no la imagen original compartida. Los endpoints antiguos siguen existiendo por compatibilidad.

ClinicMediaAssets y ClinicMediaExternalSources usan MediaItemId como PK/FK única. Todas las FKs usan RESTRICT, según la convención del proyecto. El servicio elimina explícitamente los detalles propiedad del elemento; nunca elimina Clinic ni imágenes heredadas. Los eventos de creación, edición y eliminación usan AuditLogs/IdentitySupport y su usuario autenticado, sin duplicar auditoría. Listados devuelven DTOs sin binarios ni claves de almacenamiento. Orden estable y normalizado al editar/eliminar. No hay JSON de contenido.

## Archivos
Fotos PNG/JPEG/WebP: 10 MB, MIME real verificado con el validador existente, decodificación Skia y límite de 40 megapíxeles; originales en PostgreSQL según convención existente y miniaturas WebP de hasta 640px. Texto alternativo obligatorio. Videos MP4: 50 MB, extensión/MIME y cabecera de contenedor; exportar H.264/AAC para compatibilidad de navegador. No hay transcodificación ni extracción de duración/poster; se muestra un placeholder y controles HTML al abrir. No se infieren duraciones.

## Despliegue de videos: configuración necesaria
No existe un proveedor de objetos en este proyecto. IMediaObjectStorage define el contrato y MediaObjectStorage integra un gateway HTTPS durable. No es un cliente S3 directo: requiere un gateway con PUT y DELETE por clave, autenticación Bearer y un origen HTTPS público GET con soporte Range y Content-Type video/mp4. Configurar en el servidor:

- MediaStorage__Endpoint: URL HTTPS del gateway privado.
- MediaStorage__PublicBaseUrl: origen CDN/objetos público HTTPS.
- MediaStorage__Token: secreto del gateway, nunca en Git ni en frontend.

Sin estas tres opciones la UI informa que los videos no están habilitados y la API devuelve 503. Fotos y TikTok siguen funcionando. No se escribe video a disco local/Render ni a PostgreSQL. No se ha provisionado un proveedor ni verificado una subida contra un servicio real.

Las claves son UUID generadas por el servidor. Ante fallo de DB se intenta borrar el objeto cargado. Configure alertas y reconciliación de huérfanos en el gateway para fallos simultáneos de DB/red; nunca borrar objetos de otros módulos. Al eliminar, primero se oculta la fila: si falla DELETE remoto queda oculta y reintentable. Los detalles y la fila principal se eliminan juntos en una segunda transacción con bloqueo administrativo: detalles primero, elemento al final. La imagen heredada no se elimina. DELETE del gateway debe ser idempotente (404 aceptado). La publicación no es un sistema de archivos privados: una URL ya compartida puede permanecer en caché hasta su eliminación/expiración.

## TikTok y seguridad
Solo https://www.tiktok.com/@usuario/video/ID o tiktok.com equivalente, sin credenciales ni puertos extraños; se canonicaliza y descartan query/fragment. No scraping, descarga ni embeds iniciales. Apertura externa noopener/noreferrer. Solo clinic.view permite listar administración y clinic.update modificar. Publicación filtra clínica activa y elementos visibles.

## Límites y accesibilidad
Páginas públicas de 7 y administrativas de 8, máximo API 12. Imágenes lazy, miniaturas en mosaico; original solo en diálogo. Videos preload=metadata solo al abrir, sin autoplay. Dialog nativo con Escape, foco atrapado por navegador, restauración de foco y cierre etiquetado. Móvil usa tarjetas horizontales con scroll-snap; filtros desplazables y navegación por botones. Sin animaciones automáticas.
