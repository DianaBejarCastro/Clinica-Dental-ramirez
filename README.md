# Clínica Dental RAMÍREZ · v0.1.0

Infraestructura inicial de un sistema web odontológico con monolito modular.
Incluye React, API con Controllers, Swagger, CORS, EF Core y PostgreSQL.
No incluye autenticación, módulos clínicos, tablas clínicas ni migraciones.

## Requisitos y versiones

- Node.js 24 y pnpm 11.9.0.
- .NET SDK 10.0.400 (global.json permite únicamente parches de esa banda).
- ASP.NET Core 10, Entity Framework Core 10 y proveedor Npgsql 10.
- PostgreSQL 18.
- Docker con motor Linux en ejecución y Docker Compose.
- Git.

Las imágenes son node:24-alpine, mcr.microsoft.com/dotnet/sdk:10.0,
mcr.microsoft.com/dotnet/aspnet:10.0 y postgres:18. No se usa latest.
Los tags fijan la línea de versión; sus parches pueden actualizarse.
Las dependencias se fijan mediante archivos lock.

## Estructura

```text
apps/
  web/           React + TypeScript + Vite
  api/           ASP.NET Core, capas y AppDbContext
database/
  backups/       Copias locales ignoradas por Git
  scripts/       Scripts de base de datos
docs/
  architecture/  Decisiones y responsabilidades
  database/      Persistencia y cuidados del volumen
infra/           Notas del entorno
packages/        Futuros paquetes compartidos
```

## Desarrollo local (PowerShell)

Desde la raíz:

```powershell
Copy-Item .env.example .env
```

Editar .env y reemplazar POSTGRES_PASSWORD por una contraseña local.
JWT_SECRET está reservado: no se utiliza y no habilita autenticación.
No guardar .env en Git.

Levantar PostgreSQL:

```powershell
docker compose up -d postgres
```

Frontend, en otra terminal:

```powershell
cd apps/web
pnpm install
pnpm dev
```

También se admiten pnpm install, pnpm dev y pnpm build desde la raíz.
VITE_API_URL permite cambiar la URL de la API (valor público, nunca un secreto).
Por defecto es http://localhost:5000.

API, desde la raíz en otra terminal:

```powershell
# Cargar las variables de la plantilla local sin ejecutarla como código.
Get-Content .env | ForEach-Object {
  if ($_ -match '^([A-Z_][A-Z0-9_]*)=(.*)$') {
    [Environment]::SetEnvironmentVariable($matches[1], $matches[2], 'Process')
  }
}
# El hostname postgres solo se resuelve dentro de Docker.
$env:POSTGRES_HOST = 'localhost'
cd apps/api
dotnet restore
dotnet build --no-restore
dotnet run --no-build
```

La API no lee .env automáticamente: Compose sí, y el comando anterior lo importa para dotnet.
Como alternativa, configurar ConnectionStrings__DefaultConnection.
La API rechaza una contraseña vacía o CHANGE_ME. No aplica migraciones al arrancar.
Si se personalizan puertos en .env para ejecución local, ajustar también la URL de dotnet
(--urls), el puerto de Vite (--port), VITE_API_URL y Cors__AllowedOrigins__0.
No poner secretos en variables VITE_*.

## Entorno completo en Docker

Desde la raíz, después de preparar .env:

```powershell
docker compose config --quiet
docker compose build
docker compose up -d
docker compose ps
docker compose logs -f api
docker compose down
```

Compose usa Development y Vite: es un entorno local, no una configuración de producción.
Los puertos publicados se limitan a 127.0.0.1.
La base usa un volumen persistente. No ejecutar down -v si se desea conservarlo.
Los cambios de código requieren docker compose up -d --build.

Validar la plantilla sin crear .env:

```powershell
docker compose --env-file .env.example config --quiet
```

Esto solo valida la configuración; CHANGE_ME no sirve para arrancar la API.

## URLs y puertos

| Servicio | Dirección |
| --- | --- |
| Frontend | http://localhost:5173 |
| API e información de versión | http://localhost:5000 |
| Health del proceso | http://localhost:5000/health |
| Conectividad PostgreSQL | http://localhost:5000/health/ready |
| Swagger (Development) | http://localhost:5000/swagger |
| OpenAPI (Development) | http://localhost:5000/swagger/v1/swagger.json |
| PostgreSQL | localhost:5432 |

/health devuelve 200 cuando el proceso está disponible.
/health/ready devuelve 200 con PostgreSQL accesible y 503 si falla la conexión.
El indicador del frontend consulta /health, no certifica la conexión a la base.

## Comprobaciones

```powershell
pnpm --dir apps/web install
pnpm build
dotnet restore apps/api/ClinicaRamirez.Api.csproj
dotnet build apps/api/ClinicaRamirez.Api.csproj --no-restore
docker compose --env-file .env.example config --quiet
docker compose --env-file .env.example build
```

## Colaboración y próximos pasos

Consultar AGENTS.md y docs/architecture/README.md.
Trabajar desde develop, proponer cambios en ramas y revisarlos antes de integrar a main.
La siguiente fase deberá acordar límites de módulos, esquema y auditoría antes de implementar negocio.
