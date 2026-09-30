# Clínica Dental RAMÍREZ

Sistema web de gestión odontológica.

## Stack
- React, TypeScript, Vite
- Node 24, pnpm 11.9.0
- C#, ASP.NET Core 10, Entity Framework Core 10
- PostgreSQL 18
- Docker, Docker Compose

## Reglas
- No cambiar versiones principales sin aprobación.
- No eliminar migraciones existentes.
- No guardar secretos en Git.
- No modificar main directamente. Trabajar en develop o ramas de trabajo.
- Usar DTOs. No exponer entidades de EF directamente.
- Toda modificación clínica sensible debe ser auditable.
- Un doctor puede tener múltiples especialidades.
- Un doctor puede trabajar en múltiples sucursales.
- Los horarios pertenecen a doctor + sucursal.
- No sobrescribir historial clínico.
- Mantener módulos desacoplados.
- Arquitectura de monolito modular; frontend separado del backend.
- No incorporar microservicios, Redis, RabbitMQ ni Kubernetes.
- La fase v0.1.0 incluye únicamente infraestructura: sin autenticación, migraciones ni módulos clínicos.
