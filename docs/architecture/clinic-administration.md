# Administración de clínica v0.3.0

El sistema administra una sola clínica. No hay partición por tenant ni una clínica por sucursal.
El módulo administrativo utiliza la API, PostgreSQL, autorización y auditoría de identidad existentes.
Los catálogos de permisos clínicos siguen siendo solo catálogo; esta fase no agrega pacientes, citas ni atención clínica.

## Identidad y profesión

User conserva nombre, apellido, email, contraseña, roles y estado de acceso.
DoctorProfile agrega los datos profesionales y tiene una FK única a User.
Crear un doctor admite exactamente un UserId existente o los datos de un usuario nuevo.
La operación crea o vincula el usuario y garantiza el rol DOCTOR en una transacción.
No duplica una cuenta cuando el email ya existe: se debe usar el modo de vinculación.

User.IsActive controla el acceso al sistema; DoctorProfile.IsActive controla la actividad profesional.
Desactivar al doctor no elimina ni desactiva automáticamente su cuenta. Desactivar una cuenta sí revoca
sus sesiones, conforme al comportamiento de identidad existente.

Las especialidades y sucursales se asignan mediante tablas puente. Los arrays de IDs de los DTOs son
instrucciones de asignación y no se persisten como arrays o JSON. La sustitución valida todos los IDs
antes de confirmar y elimina duplicados de entrada.

## Horarios

Son bloques semanales locales, no citas ni fechas de atención. DayOfWeek usa ISO: lunes=1 a domingo=7.
StartTime y EndTime son horas del día sin zona horaria; los timestamps de auditoría son UTC.
La interpretación local corresponde actualmente a America/La_Paz. No se representan turnos que crucen medianoche;
deben dividirse en bloques por día.

Cada horario corresponde a Doctor + Center y requiere una asignación DoctorCenter activa.
El inicio es anterior al fin. Un descanso habilitado exige inicio y fin estrictamente interiores al bloque.
Con HasBreak=false ambos valores son null. Se permiten varios bloques diarios y extremos adyacentes.
Los intervalos se comparan como [inicio, fin); el descanso no libera al doctor para trabajar en otra sucursal.
La detección de conflicto incluye todas las sucursales del doctor y devuelve 409.
La validación de frontend ayuda a corregir el formulario; el backend es la autoridad.

## Autorización y auditoría

Se reutilizan RequirePermission, JWT, consulta relacional de permisos y cookie de refresh HttpOnly.
Cada operación usa los permisos clinic.*, centers.*, doctors.*, specialties.* o schedules.* existentes.
Las acciones del frontend se muestran según permiso y los endpoints vuelven a validarlo.
El resumen solo devuelve contadores de los módulos que el usuario tiene permiso de consultar; los demás son null.

AuditLogs registra clinic_updated; center_created, center_updated, center_status_changed;
doctor_created, doctor_updated, doctor_status_changed, doctor_centers_updated, doctor_specialties_updated;
specialty_created, specialty_updated, specialty_status_changed; schedule_created, schedule_updated,
schedule_status_changed. Las escrituras y su auditoría se confirman conjuntamente.
No se registran contraseñas, tokens ni los datos profesionales completos en MetadataJson.

## Operación

La migración se aplica explícitamente. El seed de clínica se ejecuta mediante --seed-clinic y se puede repetir.
Crea la clínica inicial y doce especialidades; no crea doctores ni sucursales ficticias.
Los scripts E2E sí generan datos sintéticos identificables, que conservan desactivados al finalizar.
Se conservan el volumen y la migración InitialIdentity; no se ejecutan EnsureCreated, EnsureDeleted ni migración automática.
