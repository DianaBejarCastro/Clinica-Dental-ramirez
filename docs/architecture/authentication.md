# Autenticación y autorización v0.2.0

Identidad es un módulo del monolito ASP.NET Core; React está separado. Controllers exponen DTOs, Application implementa los casos de uso y Persistence conserva el modelo relacional. No hay módulos clínicos implementados.

## Sesiones

PasswordHasher de ASP.NET Core genera y verifica hashes de contraseñas con salt y permite rehash. SHA-256 se utiliza exclusivamente para los refresh tokens aleatorios de 48 bytes, nunca para contraseñas.

JWT HS256 contiene sub, user_id, email, sid, jti y roles. Se valida firma, algoritmo, issuer, audience y vencimiento (15 minutos por defecto, tolerancia 10 segundos). Cada solicitud autenticada comprueba en PostgreSQL que usuario y sesión continúan activos. Los permisos se consultan en la base mediante UserRoles → Roles activos → RolePermissions → Permissions; los claims de roles no sustituyen esta comprobación.

El refresh dura 7 días por defecto. Solo su hash se persiste. Cada renovación revoca el anterior y enlaza el reemplazo. Reutilizar un token revocado revoca todas las sesiones del usuario. Logout también revoca todas sus sesiones. Login, refresh, logout y desactivación serializan cambios de sesión por usuario mediante advisory locks transaccionales PostgreSQL; refresh además bloquea la fila consumida.

Cookie clinic_refresh: HttpOnly, SameSite=Strict, Path=/api/auth, Secure fuera de Development. Los POST de autenticación exigen X-Clinic-Request: 1 y rechazan Origin ajenos a la lista CORS. CORS permite credenciales solo para orígenes explícitos. Producción requiere HTTPS y frontend/API en un contexto compatible con SameSite; Compose es exclusivamente desarrollo local.

React conserva el access token en memoria; nunca usa localStorage para tokens. Al abrir la aplicación restaura sesión mediante cookie; ante 401 intenta una renovación y reintenta una vez. Una promesa compartida evita rotaciones concurrentes en la misma pestaña. Varias pestañas que roten simultáneamente pueden disparar detección de reutilización y exigir nuevo login: se prioriza revocación segura. Un fallo de logout se comunica sin fingir que la sesión del servidor fue cerrada.

## Bootstrap y permisos

El comando explícito --seed-identity crea seis roles y 56 permisos idempotentemente. Solo crea el administrador si Users está vacío y BOOTSTRAP_ADMIN_EMAIL/PASSWORD son válidos. Nunca sobreescribe usuarios ni contraseñas. No se ejecutan seed ni migraciones al arrancar el servidor.

ADMINISTRATOR recibe todos los permisos; SECRETARY recibe pacientes, agenda y pagos según catálogo; DOCTOR recibe lectura de pacientes y permisos clínicos; PATIENT no tiene permisos administrativos; CASHIER tiene lectura/creación de pagos; DENTAL_ASSISTANT tiene lectura de pacientes, agenda e inventario. Estos son permisos reservados; no habilitan módulos clínicos aún inexistentes.

Los permisos modificados de roles existentes no se reponen al repetir seed, excepto ADMINISTRATOR, que conserva el catálogo completo. Se impide desactivar ese rol, quitarle permisos o dejar sin administrador activo al sistema. No existen endpoints DELETE de usuarios o roles; las bajas son lógicas.

## Auditoría

Se registran login_success, login_failed, logout, refresh_token_rotated, refresh_token_reuse_detected, user_created, user_updated, user_status_changed, user_roles_updated, role_created, role_updated y role_permissions_updated. Actor nullable, fecha UTC, entidad, IP y user-agent limitado. MetadataJson es auxiliar: estado y referencias de roles/permisos, sin snapshots completos ni credenciales.

La aplicación impide modificar/eliminar AuditLog mediante SaveChangesAsync. Esta protección no sustituye privilegios de base de datos: un administrador PostgreSQL puede modificar directamente tablas. No se registran contraseñas, hashes de contraseña, JWT ni tokens en auditoría. Usuarios, roles y auditoría no se eliminan mediante la aplicación.

## Endpoints

| Grupo | Rutas |
| --- | --- |
| Auth | POST /api/auth/login, /refresh, /logout; GET /api/auth/me |
| Admin Users | GET/POST /api/admin/users; GET/PUT /api/admin/users/{id}; PATCH /{id}/status; PUT /{id}/roles |
| Admin Roles | GET/POST /api/admin/roles; GET/PUT /api/admin/roles/{id}; PUT /{id}/permissions |
| Admin Permissions | GET /api/admin/permissions |

Swagger permite Bearer JWT. Para POST Auth enviar también X-Clinic-Request: 1. Usuarios requiere users.view/create/update/disable según operación; asignar roles requiere users.update y roles.assign_permissions. Roles requiere roles.view/create/update/assign_permissions. Catálogo requiere permissions.view. Sin token: 401; sin permiso: 403.
