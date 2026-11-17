# Deployment preparation: Neon + Render + Vercel

This repository remains usable with `docker compose up -d --build`. Nothing in this preparation creates cloud resources, migrates a remote database or uploads development data. Do not deploy development patient data without the clinic's explicit authorization.

## Backend / Render

Use a Docker Web Service, repository root as Docker context, `apps/api/Dockerfile`. The existing multi-stage Dockerfile builds with the .NET 10 SDK and runs with the non-root ASP.NET 10 runtime. Do not replace the image with an SDK runtime. Set `ASPNETCORE_ENVIRONMENT=Production`; the image also defaults to ASP.NET Production when this is omitted. `PORT`, when supplied by Render, overrides the HTTP listener to `0.0.0.0:PORT`. Without PORT, the existing container listener remains 8080. Render terminates HTTPS; the container receives HTTP. No blanket trust of forwarded headers is enabled. If client-IP rate limits are required behind the proxy, configure trusted proxy networks explicitly before enabling forwarded IP processing.

Set these environment variables privately in Render:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | Npgsql connection string for Neon, not a PostgreSQL URL |
| `JWT_SECRET` | Independently generated random secret, at least 48 characters |
| `JWT_ISSUER`, `JWT_AUDIENCE` | Stable application identifiers |
| `JWT_ACCESS_TOKEN_MINUTES` | 1–60, default 15 |
| `JWT_REFRESH_TOKEN_DAYS` | 1–30, default 7 |
| `Cors__AllowedOrigins__0` | Exact HTTPS frontend origin, no trailing slash/path/wildcard |
| `Cors__AllowedOrigins__1` | Optional additional explicitly approved origin |
| `AuthCookies__CrossSite` | `true` only for a cross-site frontend/API pairing; otherwise false |
| `DataProtection__KeysPath` | Optional absolute path on a durable, restricted volume; see storage below |
| `GoogleAuth__ClientId` | Leave unset for now; future Google web client ID |

Do not set bootstrap credentials in the long-running production service. Initial administrative provisioning uses the existing explicit seed command in a controlled maintenance session, with temporary secure bootstrap variables. Never seed over restored production/development data without reviewing the existing records and seed behavior.

Health checks: `/health` is liveness (200 without secrets); `/health/ready` additionally checks PostgreSQL connectivity. A suspended Neon compute can increase readiness latency. Configure Render's health check to `/health`; verify `/health/ready` separately before enabling traffic. Neither endpoint applies migrations.

## Database / Neon

Runtime and EF design-time both give `ConnectionStrings__DefaultConnection` precedence over `POSTGRES_*`. An illustrative template, containing **no usable secret**, is:

```text
Host=<neon-host>;Port=5432;Database=<database>;Username=<role>;Password=<secret>;SSL Mode=VerifyFull;GSS Encryption Mode=Disable
```

Use the host/database/role supplied by Neon; never disable certificate validation or use `Trust Server Certificate=true`. The installed Npgsql/EF versions are unchanged. Use the **direct** Neon connection for migrations and dump/restore. Start with a direct runtime connection too; only adopt a pooled endpoint after checking the application's transaction/advisory-lock behavior and capacity. Do not assume local `postgres` DNS works on Windows or in Render.

Choose ONE initial database procedure, in a separately authorized deployment:

### Empty database

1. Provision an empty database with a compatible PostgreSQL version (local is PostgreSQL 18).
2. Supply the direct connection securely in the maintenance process environment.
3. From the repository using the SDK and matching `dotnet-ef`, run `dotnet ef migrations list --project apps/api`, review pending changes, then `dotnet ef database update --project apps/api`.
4. Alternatively build an EF migration bundle on a compatible platform and run it as an explicit release operation. The runtime Docker image intentionally has no SDK/EF CLI.
5. Provision the initial administrator using the existing explicit seed workflow only if necessary. Check health and login before switching traffic.

### Preserve the existing database

1. Stop application writes during the final consistent export/cutover. Back up securely using PostgreSQL 18 client tools and `pg_dump --format=custom --no-owner --no-acl --file=<private-backup>` with connection variables or a protected password file, never a password in command history.
2. Restore to a NEW, empty target using `pg_restore --no-owner --no-acl --exit-on-error --dbname=<target> <private-backup>`. Do not use `--clean`, truncate tables, or restore data on top of a separately migrated/seeded schema. Full dumps include `__EFMigrationsHistory` and the image bytea data.
3. Review role/extension requirements, migration history, constraints, counts and representative content checksums. Apply only migrations still pending after restoration.
4. Preserve Users, roles, clinical records, appointments, inventory, payments and history. Protect the backup as sensitive patient data; never put it in Git or public artifacts. Retain the original database until verification and explicit cutover approval.

Local host tooling uses `localhost` plus the published Docker PostgreSQL port (`docker compose port postgres 5432`, currently 5434 in this development environment). Container-to-container connections continue using `postgres:5432`. No automatic startup/request migrations were added.

## Frontend / Vercel

Set **Root Directory = `apps/web`**, framework Vite, Node **24.x**, build `pnpm build`, output `dist`. Keep the repository's pnpm 11.9.0/workspace lockfile; enable access to files outside the Root Directory for the workspace and use `pnpm install --frozen-lockfile` (Vercel detects the workspace root). `apps/web/vercel.json` contains the static SPA fallback. No Vercel backend/serverless function is required.

Build-time variables:

| Variable | Value source |
|---|---|
| `VITE_API_BASE_URL` | Public HTTPS Render origin, without trailing slash |
| `VITE_API_URL` | Backward-compatible existing alias; BASE_URL takes precedence |
| `VITE_GOOGLE_CLIENT_ID` | Leave unset; Google button remains coming soon |
| `VITE_GOOGLE_MAPS_API_KEY`, `VITE_GOOGLE_MAPS_MAP_ID` | Existing optional browser-restricted Maps configuration |

Vite variables are public, compiled into the bundle, and require a rebuild when changed. Never put database credentials/JWT secrets in any `VITE_*` variable. Local API fallback remains `http://localhost:5000`; production must explicitly set the HTTPS URL.

The application uses hash routing (`/#/login`, `/#/patient`, `/#/doctor/appointments`, etc.), not React Router history routing. Existing links remain intact. Direct paths such as `/login`, `/patient` and `/doctor` are served through the fallback and read by the route hook when no hash is present; refresh works. Administrative routes retain their existing names (`/`, `/clinic`, `/users`, etc.); this preparation does not invent a new `/admin` workspace. Verify direct paths, refresh and hashed booking query strings in deployment preview.

## Sessions across origins

Access JWTs live only in JavaScript memory. Refresh tokens are random, hashed in PostgreSQL, delivered as HttpOnly cookies scoped to `/api/auth`; the frontend sends `credentials: include`. No refresh token is placed in localStorage or response JSON.

Default cookies remain `SameSite=Strict`. For unrelated `*.vercel.app` and `*.onrender.com` sites, explicitly enable `AuthCookies__CrossSite=true`, which uses `SameSite=None` **and Secure** for refresh and Google nonce cookies. Production always uses Secure cookies. CORS allows only explicit configured HTTPS origins with credentials. The existing custom `X-Clinic-Request` header/origin validation remains in place; no wildcard-with-credentials policy is introduced.

**Browser limitation:** third-party-cookie restrictions can still prevent refresh on unrelated sites even with SameSite=None. For reliable production sessions, prefer clinic-owned same-site subdomains (e.g. frontend and API under the same registered domain), using Strict cookies, or separately review a same-origin proxy arrangement. Do not work around blocked cookies by exposing refresh tokens to JavaScript. Test login, refresh after reload and logout on the intended production browser/domain combination before launch. Preview origins must be explicitly allowlisted; never allow every Vercel preview automatically.

## Storage and Google (future)

Clinic logos, social-network logos, home images and staff photos are PostgreSQL binary data (`LogoData`/`ImageData`), not files in the API container. No application upload directory was found. Their persistence follows database backups and Neon storage capacity.

ASP.NET Data Protection keys can default to the container filesystem. Render's ephemeral filesystem loses these keys on restart/redeploy, invalidating outstanding protected Google nonce cookies; they are NOT the signing source of the application's JWT/refresh sessions. Google remains unconfigured now. Before enabling it, provision durable shared, access-restricted key storage (optional `DataProtection__KeysPath` for a supported persistent disk, with suitable encryption/access controls). Render Free does not provide a durable local upload/key-store solution. Logs are stdout/stderr and should use platform retention, not local files. No new storage service/table was introduced.

Future GIS setup requires the exact HTTPS frontend origin in Google's authorized JavaScript origins, plus localhost for development, with the same web client ID on frontend/backend. Do not configure it during this preparation.

Official references: [Render Docker web services and port binding](https://render.com/docs/web-services), [Vercel monorepos](https://vercel.com/docs/monorepos), [Vercel build configuration](https://vercel.com/docs/builds/configure-a-build), [Neon connection security](https://neon.com/docs/connect/connect-securely), [Google GIS setup](https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid).
