# Verificación de infraestructura v0.1.0

Fecha: 2026-09-29.

## Entorno
Carpeta inicialmente vacía, sin repositorio Git.
Node 24.15.0, pnpm 11.9.0, SDK .NET 10.0.400 y Docker Compose v5.5.1.

## Comandos y resultados
- pnpm install en apps/web: correcto tras autorizar exclusivamente el script de esbuild.
- pnpm build desde la raíz: correcto, TypeScript y Vite.
- dotnet restore en apps/api: correcto.
- dotnet build --no-restore en apps/api: correcto, 0 errores y 0 advertencias.
- docker compose --env-file .env.example config --quiet: correcto.
- docker compose --env-file .env.example build: intentado, no pudo conectarse al motor Linux de Docker Desktop.
- Pruebas HTTP con procesos temporales: raíz API y versión 0.1.0, /health, Swagger,
  documento OpenAPI, CORS permitido y rechazado, Vite y entrada React: correctos.
- /health/ready sin PostgreSQL: 503 esperado. No se ha verificado conexión con PostgreSQL real.
- Los procesos temporales se detuvieron al finalizar. No se dejó .env ni se ejecutaron migraciones.

## Correcciones
pnpm 11.9.0 rechazó inicialmente el script postinstall de esbuild.
Se configuró allowBuilds.esbuild: true en pnpm-workspace.yaml; la segunda instalación finalizó correctamente.

NuGet detectó NU1903 en Microsoft.OpenApi 2.3.0, dependencia transitiva de Swagger.
Se fijó Microsoft.OpenApi 2.7.5, conservando la versión principal y eliminando la advertencia.
Referencia: https://github.com/advisories/GHSA-v5pm-xwqc-g5wc

## Git
Se inicializa el repositorio con un commit vacío compartido por main y develop.
La infraestructura se registra en develop; main conserva únicamente el punto inicial.
No se configuran remotes ni se publica el repositorio.

## Pendiente del entorno
Iniciar Docker Desktop con el motor Linux, preparar .env según README,
ejecutar docker compose build y docker compose up -d.
Comprobar /health/ready con PostgreSQL en ejecución.
La construcción de imágenes y la conexión real a la base no están certificadas todavía.
No continuar con módulos funcionales en esta fase.
