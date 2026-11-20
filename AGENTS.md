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
- La limitación de infraestructura de v0.1.0 es histórica. El backend declara v0.3.0 y ya incluye autenticación JWT, migraciones EF Core y módulos clínicos; revisar la implementación vigente antes de proponer cambios.


## Ingeniería y coordinación de agentes
- Usar arquitectura limpia y separación de responsabilidades dentro del monolito modular. Aplicar DRY, KISS y SOLID cuando aporten claridad; evitar código muerto, capas y abstracciones innecesarias.
- Reutilizar componentes, servicios y patrones existentes antes de crear otros. Preservar contratos API, comportamiento de datos y controles de autorización salvo cambios explícitamente incluidos en la tarea.
- No leer ni modificar archivos de entorno, credenciales o secretos sin autorización explícita. No incluirlos en salidas ni artefactos.
- Inspeccionar el estado inicial y preservar cambios preexistentes. No resetear, restaurar, descartar cambios, cambiar de rama, hacer commit, merge ni push sin autorización explícita.
- Usar únicamente los roles necesarios entre software-architect, frontend-engineer, backend-engineer, ui-ux-designer, security-engineer, qa-engineer y code-reviewer. No crear roles adicionales por defecto.
- El coordinador define alcance, criterios de aceptación, contratos y propietario de cada archivo antes de delegar. Paralelizar solo trabajo independiente; nunca asignar escrituras simultáneas del mismo archivo. Los contratos, migraciones y archivos compartidos tienen un único propietario.
- Separar implementación y revisión. Los revisores entregan hallazgos al implementador; no corrigen archivos mientras los revisan. Las restricciones de un rol no sustituyen el sandbox ni los permisos de la sesión.
- Cada entrega incluye archivos afectados, decisiones, comandos realmente ejecutados, resultados y limitaciones. Ejecutar pruebas enfocadas pertinentes; no afirmar resultados de pruebas o comprobaciones visuales no realizadas.
- Elegir comandos de validación a partir de los scripts actuales del repositorio. No ejecutar pruebas contra datos de producción ni alterar bases de desarrollo para validar una tarea.

## Flujo equilibrado
- Usar un especialista principal en tareas normales. Delegar solo por una especialidad distinta necesaria o una revisión independiente justificada; no invocar todos los agentes por defecto.
- Enrutamiento: software-architect para arquitectura y diseño transversal; frontend-engineer para React, TypeScript y CSS; backend-engineer para ASP.NET Core, C#, EF Core y PostgreSQL; ui-ux-designer para diseño visual. Incorporar security-engineer para riesgos de seguridad, qa-engineer para regresión automatizada y code-reviewer para revisión independiente cuando corresponda.
- Reutilizar contexto, instrucciones y archivos ya inspeccionados mientras sigan vigentes. Comenzar por el área mínima relevante y ampliar solo por dependencias; evitar barridos completos, capturas innecesarias y logs extensos.
- Realizar el cambio completo y mantenible más pequeño, reutilizando también contratos y estilos existentes. Validar primero de forma enfocada; ampliar pruebas para cambios transversales o de alto riesgo. Repetir builds o comprobaciones solo ante nuevos cambios, fallos o dudas pendientes.
- Los cambios en autenticación, autorización, datos de pacientes, pagos, migraciones o acciones destructivas requieren revisión de Security Engineer o QA Engineer según el riesgo, y de ambos cuando proceda; no cerrarlos como verificados si falta esa revisión.
- Mantener los informes finales concisos y en español, con las evidencias y limitaciones exigidas arriba.

## Instrucciones compartidas y descubrimiento
- AGENTS.md es la fuente común del proyecto. Las responsabilidades de los roles viven en .github/agents/*.agent.md; los adaptadores .codex/agents/*.toml remiten a ellas sin duplicarlas.
- Las Skills canónicas viven en .github/skills/. Los adaptadores .agents/skills/ cargan esos mismos archivos para Codex. Leer el contenido canónico antes de aplicar una Skill; no basta su descripción.
- En Codex, invocar las Skills mediante $nombre o el selector de Skills. Las menciones /nombre en los archivos canónicos corresponden a Copilot. Cargar Skills solo cuando su alcance sea pertinente: dental-brand-identity para identidad visual del sitio público; responsive-design para layout y adaptación a pantallas; web-accessibility para accesibilidad e interacciones.
