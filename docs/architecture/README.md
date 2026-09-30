# Arquitectura v0.1.0

Monolito modular: una API ASP.NET Core y una base PostgreSQL, con frontend React separado.
Esta fase crea las capas compartidas; los módulos de negocio se diseñarán posteriormente.

- Controllers: HTTP, validación de entrada y DTOs.
- Application: futuros casos de uso y contratos.
- Domain: futuras reglas de negocio, sin dependencias de HTTP o persistencia.
- Infrastructure: integraciones técnicas y comprobación de conectividad.
- Persistence: AppDbContext y futuras configuraciones EF.
- DTOs: contratos públicos; no publicar entidades de persistencia.
- Middleware: futuras funciones transversales.

La API registra EF Core y Npgsql. No ejecuta EnsureCreated, Migrate ni SQL de esquema.
GET /health informa de la disponibilidad del proceso. GET /health/ready verifica PostgreSQL
mediante EF Core sin crear tablas. Swagger se habilita únicamente en Development.

En futuras fases, la auditoría clínica deberá diseñarse junto a cada caso de uso sensible:
actor, fecha UTC, operación, entidad, cambios y trazabilidad. El historial clínico no se sobrescribe.
Esta versión no implementa auditoría ni autenticación.

Usar ramas de trabajo desde develop y revisión antes de integrar a main.
Conservar pnpm-lock.yaml y packages.lock.json para instalaciones reproducibles.
