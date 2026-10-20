# Eliminación definitiva protegida

La única entrada es `GET /api/admin/deletion/{entity}/{id}/check` y
`DELETE /api/admin/deletion/{entity}/{id}` con `{ "password": "..." }` en el cuerpo.
No se envían contraseñas en URLs. Solo el usuario activo con rol ADMINISTRATOR
activo puede operar; se valida su contraseña actual mediante PasswordHasher.
El límite predeterminado es 5 intentos DELETE por usuario/minuto (configuración
HARD_DELETE_RATE_LIMIT; las pruebas aisladas amplían este límite).

## Clasificación completa del modelo

No hay entidades con borrado incondicional (categoría A vacía).

| Entidad / clave API | Categoría B: condiciones adicionales | Evento |
| --- | --- | --- |
| Center / centers | Inactiva | center_deleted |
| Specialty / specialties | Inactiva y no inicial del seed | specialty_deleted |
| DoctorProfile / doctors | Inactivo | doctor_deleted |
| DoctorSchedule / schedules | Inactivo | schedule_deleted |
| PatientProfile / patients | Inactivo | patient_deleted |
| Appointment / appointments | CANCELLED o NO_SHOW | appointment_deleted |
| ClinicSocialNetwork / social-networks | Inactiva | clinic_social_network_deleted |
| User / users | Inactivo; nunca el actor ni el último administrador activo | user_deleted |
| Role / roles | Inactivo; IsSystem=false | role_deleted |

Todos requieren **cero referencias entrantes**, incluidas las inactivas/históricas.
Una FK saliente no elimina ni modifica el registro padre. Por ejemplo, eliminar
un perfil de doctor libre no elimina su cuenta de usuario.

Categoría C, siempre protegida: Clinic raíz, AuditLog, Permission, roles IsSystem,
especialidades iniciales (código o auditoría de seed), RefreshToken, UserRole,
RolePermission, DoctorCenter, DoctorSpecialty, historial de migraciones y toda
entidad/configuración no incluida explícitamente en el registro del servicio.
Las asignaciones siguen sus flujos habituales; este API nunca borra relaciones
para poder borrar un padre. No se cambia ningún guard existente de SaveChanges.

## Dependencias y atomicidad

DeletionGuardService obtiene tabla/esquema desde EF y consulta las FK reales en
pg_constraint, contando filas por tabla referenciante. Para Users incluye perfiles,
sesiones, auditoría, creador de citas y ambos vínculos de UserRoles. Para Centers
incluye DoctorCenters, DoctorSchedules y Appointments. DoctorProfiles, Specialties,
PatientProfiles y Roles se comprueban con el mismo mecanismo, sin listas parciales.
Las FK compuestas o con acciones distintas de RESTRICT bloquean conservadoramente.
No se cambian ni se eliminan constraints.

DELETE toma el bloqueo administrativo compartido y FOR UPDATE sobre la fila,
recarga el registro, repite autorización/estado/dependencias y ejecuta un DELETE
parametrizado en la misma transacción que la auditoría. Los nombres de tabla solo
proceden del modelo y registro permitido; todos los identificadores se entrecomillan.
23503 (FK) y 23001 (RESTRICT en PostgreSQL 18) se transforman en 409 estructurado
y rollback. También se revierte el borrado
si falla la auditoría. La auditoría contiene actor y entityId/entityType/deletedAt,
sin contraseña ni copia de datos clínicos. EntityId no es FK al registro eliminado.

El frontend oculta acciones no autorizadas y usa ProtectedDeleteButton/Dialog.
La comprobación previa es informativa: el backend vuelve a decidir al confirmar.
La contraseña solo vive en memoria del modal, se limpia al cerrar o responder y
nunca se registra. No se piden credenciales si hay dependencias o protección.

## Convención para futuras entidades

La política predeterminada es prohibir. Antes de habilitar una entidad hay que:

1. Declarar categoría y reglas de estado en DeletionGuardService.
2. Crear todas las FK con RESTRICT y documentar dependencias del dominio que no
   estén representadas por FK; si existen, agregarlas a Inspect antes de habilitar.
3. Mantener autorización ADMINISTRATOR y contraseña del actor.
4. Declarar evento de auditoría y probar fila eliminada, bloqueos y rollback.
5. Integrar el componente común; nunca crear DELETE independientes sin guard.

Las pruebas destructivas usan exclusivamente bases identity_test_* aisladas.
No se eliminan registros de desarrollo para verificar esta función.
