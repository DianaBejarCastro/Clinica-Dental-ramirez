# Verificación de identidad relacional v0.2.0

Fecha: 2026-09-30 (America/La_Paz).

## Resultado

Fase de identidad completada sobre el código parcial existente. Se conservó la migración inicial y el volumen de desarrollo. Backend, frontend y Docker ejecutan v0.2.0. No se implementaron módulos clínicos, sucursales, doctores, agenda ni odontograma.

## Base de datos

Migración 20260930042300_InitialIdentity aplicada explícitamente a clinica_ramirez, registrada en __EFMigrationsHistory con ProductVersion 10.0.3. `dotnet ef migrations has-pending-model-changes --project apps/api --no-build` confirmó ausencia de diferencias antes de aplicar.

Tablas: Users, Roles, Permissions, UserRoles, RolePermissions, RefreshTokens y AuditLogs, más __EFMigrationsHistory. Verificación directa mediante information_schema, pg_constraint y pg_indexes: PK simples y compuestas correctas; ocho FK de aplicación con ON DELETE RESTRICT; índices únicos de email, códigos, nombre de rol y hash de token; NOT NULL y CHECK presentes. Timestamps en timestamp with time zone, escritos en UTC.

Users N:M Roles mediante UserRoles; Roles N:M Permissions mediante RolePermissions; Users 1:N RefreshTokens; actor nullable de AuditLogs. No hay relaciones JSON. [Detalle y diagrama](database/identity-schema.md).

Seed repetido desde el contenedor sin duplicación. Seis roles, 56 permisos y 85 asignaciones RolePermissions:

| Rol | Permisos |
| --- | ---: |
| ADMINISTRATOR | 56 |
| SECRETARY | 9 |
| DOCTOR | 15 |
| PATIENT | 0 |
| CASHIER | 2 |
| DENTAL_ASSISTANT | 3 |

Bootstrap creó un administrador usando variables locales; contraseña aleatoria añadida solo al .env ignorado cuando faltaba. No se sobreescribieron secretos existentes. La prueba end-to-end conserva un usuario sintético inactivo; no se eliminó ningún usuario ni volumen. Las pruebas xUnit conservan sus bases identity_test_<uuid> aisladas.

## Backend y seguridad

DTOs, PasswordHasher, JWT HS256, validación de issuer/audience/lifetime/firma y verificación de sesión activa en PostgreSQL. Access de 15 minutos, refresh de 7 días, token criptográfico aleatorio y persistencia exclusiva de su hash. Rotación, revocación y detección de reutilización verificadas. Cookie HttpOnly, SameSite=Strict, Secure fuera de Development. Permisos consultados mediante tablas puente, con roles y usuarios activos.

Login, refresh, logout y desactivación serializan cambios de sesión por usuario para evitar que una renovación concurrente sobreviva al logout. Se protege al último administrador activo. No hay DELETE físico de usuarios ni endpoint para borrar roles. No hay migración o seed automático al arrancar.

Auditoría implementada para todos los eventos solicitados y detección de reutilización. Se comprobaron en desarrollo login_success, login_failed, logout, refresh_token_rotated, user_created, user_updated, user_status_changed y user_roles_updated. Las operaciones de roles/permisos también se ejercitaron en la suite aislada. MetadataJson contiene solo datos auxiliares; la consulta de campos sensibles devolvió 0 coincidencias. [Decisiones y límites de seguridad](architecture/authentication.md).

## Pruebas

| Verificación | Resultado |
| --- | --- |
| dotnet restore | Correcto |
| dotnet build | Correcto, 0 warnings y 0 errores |
| dotnet test vía scripts/test-backend.ps1 | 19 correctas, 0 fallidas, 0 omitidas |
| pnpm install | Correcto |
| pnpm build | TypeScript/Vite correcto |
| pnpm test | 7 correctas |
| node scripts/verify-identity.mjs | 23 comprobaciones HTTP correctas |
| node scripts/verify-browser.mjs | Flujo real Edge correcto |
| docker compose config --quiet | Correcto |
| docker compose build / up -d --build | Correcto |

Backend: login correcto/incorrecto, usuario inactivo, email duplicado normalizado, acceso sin token/sin permiso/con permiso, refresh válido, reutilización y revocación del reemplazo, logout y revocación de access, bootstrap idempotente, asignación de roles/permisos con efecto inmediato, desactivación, 400/404, protección del último administrador, cabecera CSRF, auditoría sin credenciales/inmutable mediante SaveChangesAsync, refresh concurrente con logout y edición de rol.

Frontend: render login, error de login, login correcto, ruta protegida, listado usuarios, listado roles y ruta restringida por permiso. Vitest + Testing Library con respuestas HTTP simuladas; la comprobación de navegador separada usa la API y PostgreSQL reales.

E2E HTTP sobre contenedores: login administrador, me, users, roles, permissions, creación/consulta de usuario, duplicado, asignación de rol, actualización, login sin permiso administrativo, desactivación, rechazo de login y access del usuario inactivo, paginación inválida, recurso inexistente, acceso sin token, refresh, logout y revocación. Códigos comprobados: 200, 201, 204, 400, 401, 403, 404 y 409.

Edge headless con perfil aislado: React renderizó login; login real correcto; cookie clinic_refresh HttpOnly/Strict; localStorage y sessionStorage vacíos; pantallas Usuarios/Roles/Permisos; sesión restaurada después de recargar; logout regresa al login en ruta protegida. Capturas locales ignoradas en artifacts/login-v020.png y artifacts/admin-v020.png.

## Docker y HTTP

| Servicio | Imagen | Estado | Puerto local |
| --- | --- | --- | --- |
| postgres | postgres:18 | running, healthy | 127.0.0.1:5434 |
| api | clinica-ramirez-api:0.2.0 | running | 127.0.0.1:5000 |
| web | clinica-ramirez-web:0.2.0 | running | 127.0.0.1:5173 |

Los tres contenedores tienen RestartCount=0. El servidor PostgreSQL del otro proyecto no se modificó. Comunicación interna postgres:5432.

GET /, /health, /health/ready, /swagger y frontend: 200. Swagger declara versión 0.2.0 y Bearer JWT; publica Auth, Admin Users, Admin Roles y Admin Permissions. API informativa devuelve 0.2.0. Frontend incluye exclusivamente administración de identidad.

## Incidencias y correcciones

1. Compose no pasaba JWT ni bootstrap: se añadieron variables y tags 0.2.0. Plantilla completada sin secretos.
2. Concurrencia de refresh/logout/desactivación: se añadió bloqueo transaccional por usuario y prueba de carrera.
3. EF Relational se resolvía en distintas versiones al compilar pruebas: referencia explícita 10.0.3, compilación final sin advertencias.
4. Una aserción async de pruebas carecía de await: corregida antes de ejecutar la suite final.
5. PowerShell escribió BOM en package.json: normalizado a UTF-8 sin BOM; build y pruebas frontend pasaron.
6. El servidor xUnit no pudo arrancar dentro del sandbox Windows por acceso al registro de eventos: ejecución autorizada fuera del sandbox; 19/19 correctas.
7. EF consultó __EFMigrationsHistory antes de crearla en bases nuevas; PostgreSQL registra la consulta fallida inicial. La migración terminó correctamente y se confirmó el historial. No es fallo persistente.
8. ASP.NET Core emite advertencias DataProtection por claves locales del contenedor sin cifrado/persistencia. Esta fase utiliza JWT y refresh opaco, no cookies de autenticación DataProtection. Se documenta la advertencia; no hay errores críticos ni restart loops.
9. Una consulta manual por argumentos perdió comillas de Users en PowerShell; las verificaciones SQL se envían por stdin mediante el archivo versionado para preservar identificadores.

## Reproducción

```powershell
dotnet restore
dotnet build
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-backend.ps1
pnpm install
pnpm build
pnpm test
docker compose config --quiet
docker compose up -d --build
docker compose ps
node scripts/verify-identity.mjs
node scripts/verify-browser.mjs
Get-Content database/scripts/verify-identity.sql | docker compose exec -T postgres psql -U clinica_user -d clinica_ramirez -X -v ON_ERROR_STOP=1
```

## Git

Trabajo realizado en develop sobre ed04b29. HEAD se conservó hasta terminar las verificaciones. Commit de cierre: `feat: add relational identity auth roles permissions v0.2.0`. Sin merge, push ni remoto; main permanece intacta. Se revisan status, diff, diff --check, ls-files y exclusiones antes del commit. .env, bin, obj, node_modules, dist, logs y artifacts permanecen ignorados. Migración, pruebas, scripts y documentación forman parte del commit.
