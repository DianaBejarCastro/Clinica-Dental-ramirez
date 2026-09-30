# Verificación de infraestructura v0.1.1

Fecha: 2026-09-29 (America/La_Paz).

## Resultado

Infraestructura verificada con los tres servicios de Docker Compose ejecutándose simultáneamente.
PostgreSQL real accesible mediante AppDbContext y Npgsql; frontend React conectado a la API.
No se crearon tablas clínicas, migraciones, usuarios de aplicación ni autenticación.

| Servicio | Imagen | Estado | Puerto publicado |
| --- | --- | --- | --- |
| postgres | postgres:18 | running, healthy | 127.0.0.1:5434 → 5432 |
| api | clinica-ramirez-api:0.1.1 | running | 127.0.0.1:5000 → 8080 |
| web | clinica-ramirez-web:0.1.1 | running | 127.0.0.1:5173 → 5173 |

Los tres contenedores terminaron con RestartCount=0 y permanecen en ejecución.
Docker Desktop 4.91.0, Engine 29.8.0 (Linux), Compose v5.5.1.
La etiqueta v0.1.1 identifica esta fase de infraestructura; la versión del contrato informativo
de la API y los manifiestos de aplicación permanece en 0.1.0.

## PostgreSQL y EF Core

Consultas ejecutadas dentro del contenedor:
- SELECT version(): PostgreSQL 18.6 (Debian 18.6-1.pgdg13+2), x86_64.
- SELECT current_database(): clinica_ramirez.
- SELECT current_user: clinica_user.
- Consulta de information_schema.tables para public: 0 tablas.

Program.cs registra AppDbContext con UseNpgsql. La cadena se construye con
NpgsqlConnectionStringBuilder y las variables de entorno de Compose:
Host=postgres, Port=5432, Database=clinica_ramirez, Username=clinica_user y contraseña local.
DatabaseHealthCheck usa Database.CanConnectAsync; los logs muestran SELECT 1 ejecutado
contra la base real al consultar /health/ready.
No hay llamadas a EnsureCreated, EnsureDeleted ni Migrate al arrancar.

El volumen dental-clinic_postgres_data está montado en /var/lib/postgresql.
No se eliminó ni reinicializó ningún volumen existente.

## HTTP y navegador

| URL | Resultado |
| --- | --- |
| http://localhost:5000/ | 200 |
| http://localhost:5000/health | 200, Healthy |
| http://localhost:5000/health/ready | 200, PostgreSQL accesible |
| http://localhost:5000/swagger | 200 tras redirección a la interfaz |
| http://localhost:5000/swagger/v1/swagger.json | 200 |
| http://localhost:5173/ | 200 |

Se verificó Access-Control-Allow-Origin: http://localhost:5173.
Microsoft Edge headless, con un perfil temporal aislado, ejecutó React y la llamada fetch real.
El DOM resultante mostró Frontend: disponible y API: Healthy.
El indicador corresponde a /health; /health/ready verifica por separado la base.

## Comandos ejecutados

Desde la raíz:
```powershell
docker version
docker compose version
docker info
docker compose config --quiet
docker compose build
docker compose up -d
docker compose ps
docker compose exec -T postgres psql -U clinica_user -d clinica_ramirez -c 'SELECT version(); SELECT current_database(); SELECT current_user;'
docker compose exec -T postgres psql -U clinica_user -d clinica_ramirez -c "SELECT count(*) AS public_tables FROM information_schema.tables WHERE table_schema = 'public';"
docker run --rm clinica-ramirez-web:0.1.1 pnpm build
docker compose logs --no-color postgres api web
docker inspect --format '{{.Name}} status={{.State.Status}} restarts={{.RestartCount}}' dental-clinic-postgres-1 dental-clinic-api-1 dental-clinic-web-1
git status
git branch
git diff
git diff --check
git ls-files
git check-ignore .env apps/api/bin apps/api/obj apps/web/node_modules apps/web/dist
```

Se utilizó config --quiet para validar sin imprimir los secretos interpolados.
También se realizaron peticiones con Invoke-WebRequest y una comprobación del DOM con
Edge --headless --dump-dom --virtual-time-budget=10000.
Tras corregir Kerberos se ejecutaron docker compose build api y docker compose up -d,
y se repitieron todos los endpoints de la API y la revisión de logs.
La compilación TypeScript/Vite y la publicación Release de .NET dentro de Linux finalizaron correctamente.

## Incidencias y correcciones

1. El puerto 5432 estaba ocupado por dulce-postgres, ajeno a este proyecto.
   El usuario autorizó publicar esta base en 5434; se cambió solo POSTGRES_PORT en .env.
   La comunicación interna sigue en postgres:5432. No se detuvo el otro contenedor.
   La plantilla .env.example mantiene 5432 como valor predeterminado.
2. Compose asignaba implícitamente latest a las imágenes locales construidas.
   Se fijaron image: clinica-ramirez-api:0.1.1 e image: clinica-ramirez-web:0.1.1.
3. Npgsql intentaba cargar libgssapi_krb5.so.2, ausente en la imagen.
   La conexión funcionaba, pero se ajustó GssEncryptionMode.Disable únicamente para
   la cadena generada en Development, donde no se utiliza Kerberos.
   Fuera de Development se conserva Prefer; las cadenas explícitas mantienen sus opciones.
   No se alteró la configuración SSL/TLS.
   Referencia: https://www.npgsql.org/doc/security.html#gss-session-encryption-gss-api
4. Se incorporaron los textos explícitos Frontend: disponible y API: Healthy.

Logs finales: sin errores críticos ni ciclos de reinicio.
PostgreSQL muestra el aviso de trust para conexiones locales por socket durante initdb;
la conexión TCP de la API usa la contraseña configurada. El apagado del servidor temporal
de initdb es parte de la inicialización normal, no un reinicio del contenedor.

## Seguridad y Git

.env se creó desde .env.example con contraseña PostgreSQL y JWT_SECRET independientes,
generados con un generador criptográfico (48 bytes cada uno).
.env está ignorado, no se añadió al índice y no se imprimieron los secretos.
Se verificó que sus valores no aparecen en archivos versionados.
bin, obj, node_modules, dist y logs permanecen fuera de Git.
No hay credenciales de conexión hardcodeadas en el código.

Rama activa: develop. No se modificó main, no se configuró remoto y no se hizo push.
Commit previsto tras revisión:
chore: verify docker and postgres infrastructure v0.1.1

Archivos versionados modificados: apps/api/Program.cs, apps/web/src/App.tsx,
docker-compose.yml, README.md y este informe.
Archivo local creado: .env (ignorado).
