# Inventario clínico

Cinco tablas: categorías, artículos, saldos por sucursal, movimientos y líneas. Las ocho claves foráneas usan RESTRICT. No se duplican usuarios, doctores ni sucursales.

Las cantidades tienen tres decimales. Un ajuste registra una cantidad positiva y una dirección INCREASE o DECREASE en la cabecera, junto con un motivo obligatorio. Para ajustes mixtos se registran dos movimientos. Esto mantiene Quantity > 0 en cada línea sin ocultar el signo del ajuste. La unidad de un artículo con movimientos no puede cambiar; las etiquetas actuales se consultan del catálogo relacionado.

Cada operación valida los permisos actuales y la relación DoctorCenter. Los retiros del doctor resuelven su perfil desde el usuario autenticado y rechazan campos adicionales de suplantación. El formulario nunca escribe InventoryStocks directamente.

Una transacción inserta cabecera, líneas, auditoría y actualiza saldos. Los saldos se bloquean con FOR UPDATE en orden estable por artículo; INSERT ON CONFLICT permite inicializar una combinación artículo/sucursal sin carrera. Cantidades negativas están prohibidas también por PostgreSQL. La clave de idempotencia es única por usuario y se serializa mediante un advisory lock transaccional. Una repetición idéntica devuelve el mismo movimiento; reutilizar la clave con otro contenido produce 409.

Los movimientos no tienen endpoints UPDATE/DELETE. Dos triggers rechazan modificaciones y borrados de movimientos y líneas. Las correcciones usan devoluciones o ajustes. Categorías/artículos se desactivan. Los saldos iniciales se representan como cero sin crear movimientos ficticios.

La migración agrega categorías controladas y permisos a los roles existentes. El seed conserva su política previa de no reponer permisos retirados por administración. No se agregan lotes ni vencimientos en esta fase.

Pruebas: InventoryTests utiliza bases PostgreSQL aisladas del fixture; el flujo de humo cubre entrada 20, retiro administrativo 3, retiro propio 2, saldo final 15 y rechazo de sobreretiro. No inserta registros en la base de desarrollo.
