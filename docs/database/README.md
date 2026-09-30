# PostgreSQL

> Las notas siguientes corresponden a la infraestructura v0.1.x. Para las siete tablas de identidad de v0.2.0 consultar [identity-schema.md](identity-schema.md).

PostgreSQL 18, base clinica_ramirez, usuario clinica_user. Contraseña mediante entorno.
El volumen postgres_data se monta en /var/lib/postgresql para la estructura de PostgreSQL 18.
El puerto del host es configurable mediante POSTGRES_PORT; entre contenedores siempre es 5432.

AppDbContext está vacío. No hay entidades, tablas clínicas ni migraciones.
La imagen de PostgreSQL crea únicamente la base y el usuario iniciales.
Cambiar las variables de inicialización no cambia credenciales de un volumen existente.

database/scripts reserva scripts revisables. database/backups conserva solo .gitkeep en Git.
No almacenar copias clínicas o credenciales en el repositorio.
No usar docker compose down -v si se necesita conservar los datos.
