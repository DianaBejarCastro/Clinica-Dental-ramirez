# Inventario clínico

Cinco tablas: categorías, artículos, saldos por sucursal, movimientos y líneas. Las ocho claves foráneas usan RESTRICT. No se duplican usuarios, doctores ni sucursales.

Las cantidades tienen tres decimales. Un ajuste registra una cantidad positiva y una dirección INCREASE o DECREASE en la cabecera, junto con un motivo obligatorio. Para ajustes mixtos se registran dos movimientos. Esto mantiene Quantity > 0 en cada línea sin ocultar el signo del ajuste. La unidad de un artículo con movimientos no puede cambiar; las etiquetas actuales se consultan del catálogo relacionado.

Cada operación valida los permisos actuales y la relación DoctorCenter. Los retiros del doctor resuelven su perfil desde el usuario autenticado y rechazan campos adicionales de suplantación. El formulario nunca escribe InventoryStocks directamente.

Una transacción inserta cabecera, líneas, auditoría y actualiza saldos. Los saldos se bloquean con FOR UPDATE en orden estable por artículo; INSERT ON CONFLICT permite inicializar una combinación artículo/sucursal sin carrera. Cantidades negativas están prohibidas también por PostgreSQL. La clave de idempotencia es única por usuario y se serializa mediante un advisory lock transaccional. Una repetición idéntica devuelve el mismo movimiento; reutilizar la clave con otro contenido produce 409.

Los movimientos no tienen endpoints UPDATE/DELETE. Dos triggers rechazan modificaciones y borrados de movimientos y líneas. Las correcciones usan devoluciones o ajustes. Categorías/artículos se desactivan. Los saldos iniciales se representan como cero sin crear movimientos ficticios.

La migración agrega categorías controladas y permisos a los roles existentes. El seed conserva su política previa de no reponer permisos retirados por administración. No se agregan lotes ni vencimientos en esta fase.

Pruebas: InventoryTests utiliza bases PostgreSQL aisladas del fixture; el flujo de humo cubre entrada 20, retiro administrativo 3, retiro propio 2, saldo final 15 y rechazo de sobreretiro. No inserta registros en la base de desarrollo.

## Fase 2: lotes y vencimiento

- `InventoryLots` identifica un lote real por artículo y número normalizado (trim + mayúsculas). `InventoryLotStocks` conserva el saldo por lote y sucursal. Cuatro nuevas FK usan RESTRICT; no hay tablas de asignaciones ni JSON de stock.
- Los artículos existentes mantienen `TracksLots=false` y `TracksExpiration=false`. No se inventan lotes históricos. El vencimiento requiere seguimiento de lotes.
- Entradas, retiros, devoluciones y ajustes bloquean los artículos en orden estable mediante advisory locks transaccionales, además del bloqueo de saldo de fase 1. Total y detalle se guardan en una sola transacción. La inicialización y los cambios de seguimiento usan el mismo bloqueo.
- El retiro aplica FEFO por fecha y luego recepción/Id; sin vencimiento usa FIFO por recepción/Id. Excluye lotes vencidos e inactivos y genera una línea por lote utilizado. La fecha de hoy usa America/La_Paz; un lote que vence hoy todavía es utilizable.
- El total físico incluye existencias vencidas/inactivas; un retiro puede ser rechazado aunque exista saldo físico, si no existe suficiente saldo utilizable. Los ajustes administrativos con motivo permiten retirar existencias no utilizables sin un retiro clínico excepcional.
- Devoluciones y ajustes requieren seleccionar un lote real del artículo. No hay edición ni eliminación de movimientos. Corregir fecha, fabricación, proveedor o actividad del lote exige administrador, permiso y motivo auditado.
- Para habilitar lotes con stock existente, `POST /api/admin/inventory/items/{id}/initialize-lots` exige asignaciones que coincidan exactamente con el saldo de cada sucursal. La operación no cambia el total ni reescribe movimientos previos. Con saldo cero puede habilitarse desde la edición del artículo. Una vez creados lotes no se puede deshabilitar el seguimiento ni cambiar su política de vencimiento.
- La idempotencia mantiene la clave por actor. Un SHA-256 del pedido permite comparar retiros que se distribuyeron en varias líneas, sin persistir listas JSON. Los movimientos anteriores sin huella siguen utilizando la comparación de fase 1.
- `GET /api/admin/inventory/lots` filtra producto, sucursal, categoría, número, fechas y estado calculado. Las ventanas de alertas son 30/60/90 días. `GET /api/admin/inventory/expiration-alerts` devuelve indicadores calculados del backend; no se persiste un estado redundante.
- Migración: `20261027145348_AddInventoryLotsAndExpirationTracking`. Probada sobre una copia temporal con hashes de registros de fase 1 antes/después. Aplicada también a desarrollo durante la integración: conteos y checksums preservados, sin crear lotes ni movimientos de prueba.
