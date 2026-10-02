# Clínica Dental RAMÍREZ · v0.3.0

Monolito modular ASP.NET Core 10 / EF Core 10 / PostgreSQL 18 con React, TypeScript y Vite separados. Incluye identidad relacional y administración de clínica, sucursales, doctores, especialidades y horarios semanales. No incluye pacientes clínicos, citas, historia clínica, odontograma, tratamientos, inventario ni caja.

## Requisitos

Node 24, pnpm 11.9.0, .NET SDK 10.0.400 y Docker Desktop con motor Linux. No se cambiaron versiones principales. Dependencias frontend y API fijadas mediante archivos lock.

## Configuración local

Si aún no existe `.env`, copiar `.env.example`. Configurar POSTGRES_PASSWORD, JWT_SECRET (al menos 48 caracteres aleatorios) y BOOTSTRAP_ADMIN_PASSWORD (12–128 caracteres). Nunca versionar ni imprimir estos valores. No sobrescribir un `.env` existente.

Configurar POSTGRES_PORT=5434 para este equipo: otro proyecto ocupa 5432. La API dentro de Docker siempre usa postgres:5432. JWT usa issuer clinica_ramirez, audience clinica_ramirez_web, access de 15 minutos y refresh de 7 días por defecto.

```powershell
pnpm install
dotnet restore
dotnet tool restore
dotnet build
docker compose up -d postgres
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/initialize-identity.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/initialize-clinic.ps1
docker compose config --quiet
docker compose up -d --build
```

`initialize-identity.ps1` aplica explícitamente la migración y ejecuta seed contra localhost:5434. El seed crea seis roles y 56 permisos. Crea el administrador inicial únicamente si no hay usuarios, usando las variables BOOTSTRAP_ADMIN_EMAIL y BOOTSTRAP_ADMIN_PASSWORD. Se puede repetir sin duplicar datos. No se ejecutan migración ni seed al iniciar la API normalmente.

En este entorno el bootstrap local usa el email guardado en `.env`; la contraseña está exclusivamente allí. La aplicación no incluye recuperación ni cambio de contraseña en esta fase.

## Direcciones

| Servicio | URL |
| --- | --- |
| Aplicación y login | http://localhost:5173 |
| API / versión | http://localhost:5000 |
| Proceso | http://localhost:5000/health |
| PostgreSQL accesible | http://localhost:5000/health/ready |
| Swagger | http://localhost:5000/swagger |
| PostgreSQL local | localhost:5434 |

Compose usa imágenes clinica-ramirez-api:0.3.0 y clinica-ramirez-web:0.3.0. Publica solo en 127.0.0.1 y conserva el volumen postgres_data. Es desarrollo: Vite y Swagger habilitados, cookie sin Secure únicamente en Development. Producción requiere HTTPS y configuración revisada. No usar down -v para conservar datos.

## Frontend

Login, restauración de sesión, logout, rutas protegidas y menú según permisos. Access token en memoria; refresh en cookie HttpOnly. Rutas hash: `#/`, `#/users`, `#/roles`, `#/permissions`, `#/clinic`, `#/centers`, `#/doctors`, `#/specialties`, `#/schedules`. Usuarios incluye paginación, creación, edición, estado y roles; Roles incluye creación, edición y permisos; Permisos es un catálogo de solo lectura agrupado por módulo.

## Pruebas

```powershell
dotnet restore
dotnet build
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-backend.ps1
pnpm install
pnpm build
pnpm test
node scripts/verify-identity.mjs
node scripts/verify-browser.mjs
node scripts/verify-clinic.mjs
node scripts/verify-clinic-browser.mjs
Get-Content database/scripts/verify-identity.sql | docker compose exec -T postgres psql -U clinica_user -d clinica_ramirez -X -v ON_ERROR_STOP=1
```

El script backend ejecuta `dotnet test` con conexión por entorno y crea una base PostgreSQL aislada identity_test_<uuid>. No borra bases; cada ejecución conserva datos sintéticos para inspección. El servidor de pruebas puede requerir ejecución fuera del sandbox Windows.

`verify-identity.mjs` prueba la API Docker contra desarrollo, lee credenciales de `.env` sin imprimirlas y conserva un usuario sintético desactivado. Requiere migración, seed y contenedores activos. El logout invalida las sesiones del administrador usado para la verificación.

## Desarrollo fuera de Docker

Para frontend: `pnpm dev`. Para API: cargar `.env` como variables de proceso, establecer POSTGRES_HOST=localhost y POSTGRES_PORT=5434, y ejecutar `dotnet run --project apps/api`. La API no lee `.env` por sí misma. Mantener configuraciones CORS, puertos y VITE_API_URL coherentes. Nunca poner secretos en variables VITE_*.

## Documentación

- [Verificación v0.3.0](docs/verification-v0.3.0.md)
- [Modelo de administración](docs/database/clinic-administration-schema.md)
- [Reglas administrativas](docs/architecture/clinic-administration.md)
- [Verificación v0.2.0](docs/verification-v0.2.0.md)
- [Autenticación, sesiones y permisos](docs/architecture/authentication.md)
- [Modelo relacional, restricciones y migración](docs/database/identity-schema.md)
- [Verificación histórica v0.1.1](docs/verification-v0.1.1.md)

Trabajar en develop o ramas de trabajo. No modificar main directamente. Las verificaciones anteriores conservan su valor histórico y describen fases previas.

## Administración v0.3.0

`initialize-clinic.ps1` aplica explícitamente la nueva migración y ejecuta `--seed-clinic`.
El seed crea una sola clínica y doce especialidades. No genera doctores ni sucursales.
Se puede repetir sin duplicar ni restablecer las personalizaciones. No cambia InitialIdentity.
En instalaciones nuevas, ejecutar primero el seed de identidad.

El resumen muestra totales reales según permisos. Clínica, Sucursales y Especialidades tienen formularios
con validación, confirmación y estado. Doctores admite crear cuenta nueva o vincular una existente,
asignar varias especialidades/sucursales y consultar el perfil. La cuenta y el estado profesional
son independientes. Los horarios semanales usan lunes=1 a domingo=7, permiten varios bloques por día
y rechazan solapamientos del mismo doctor entre cualquier sucursal. Son horas locales de America/La_Paz.

Los cambios de estado se conservan como desactivación; no hay borrado físico de perfiles.
Los permisos se validan en servidor y las operaciones generan auditoría sin credenciales.

El E2E HTTP crea datos sintéticos identificados con sufijos aleatorios, restaura los datos de clínica
que modificó y conserva inactivos el doctor, la cuenta, las sucursales y la especialidad de prueba.
El logout del smoke afecta las sesiones del administrador de verificación. No ejecutar estos scripts
contra datos de producción. Las pruebas unitarias/integración usan una base separada.

Para inspeccionar tablas, restricciones, índices y auditoría:

```powershell
Get-Content database/scripts/verify-clinic.sql | docker compose exec -T postgres psql -U clinica_user -d clinica_ramirez -X -v ON_ERROR_STOP=1
```
