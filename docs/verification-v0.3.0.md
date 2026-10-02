# Verificación de administración de clínica v0.3.0

Fecha: 2026-09-30 (America/La_Paz).

## Punto de partida

Se confirmó develop limpia en 1cd6645, commit de cierre de identidad v0.2.0.
Se revisaron AGENTS.md, README, verificación v0.2.0, esquema de identidad, código y migración existente.
Git dentro del sandbox inicialmente rechazó la propiedad del repositorio; la inspección se hizo con
safe.directory limitado a esta ruta por comando, sin cambiar la configuración global.
Docker Desktop estaba apagado; se inició y se verificó la base existente antes de modificarla.
Se conservó una copia local ignorada en database/backups/pre-v030.sql.

## Alcance

Una sola clínica, sucursales, perfiles profesionales vinculados a usuarios, especialidades,
asignaciones N:M y horarios semanales por doctor y sucursal. Dashboard con contadores reales,
frontend administrativo y auditoría. Se mantienen identidad, JWT, refresh y permisos existentes.
No se implementaron pacientes clínicos, citas, historia clínica, odontograma, tratamientos, inventario ni caja.

## Modelo

Siete tablas nuevas: Clinics, Centers, DoctorProfiles, Specialties, DoctorSpecialties,
DoctorCenters y DoctorSchedules. UUID, FK explícitas con RESTRICT, PK compuestas para tablas puente,
índices únicos y CHECK. No se representan relaciones mediante JSON o arrays.
Ver [modelo relacional](database/clinic-administration-schema.md) y [reglas del módulo](architecture/clinic-administration.md).

## Reproducción

```powershell
dotnet restore
dotnet build
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-backend.ps1
pnpm install
pnpm build
pnpm test
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/initialize-clinic.ps1
docker compose config --quiet
docker compose build
docker compose up -d
docker compose ps
node scripts/verify-identity.mjs
node scripts/verify-browser.mjs
node scripts/verify-clinic.mjs
node scripts/verify-clinic-browser.mjs
Get-Content database/scripts/verify-clinic.sql | docker compose exec -T postgres psql -U clinica_user -d clinica_ramirez -X -v ON_ERROR_STOP=1
```

Los scripts de integración crean bases aisladas. El E2E real conserva datos sintéticos inactivos
y restaura la ficha de clínica. No se borran usuarios, volúmenes ni migraciones.
La publicación local de PostgreSQL sigue en 5434 y la conexión interna en postgres:5432.

## Resultados

Pendientes de registrar tras completar compilación, migración, suites y pruebas reales.

## Git

Trabajo en develop, sin merge, push ni remoto. Main y InitialIdentity se conservarán sin cambios.
Commit de cierre previsto solo después de todas las comprobaciones:
feat: add clinic centers doctors specialties schedules v0.3.0
