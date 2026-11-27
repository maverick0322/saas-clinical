# saas-clinical

Microservicio **Clinical** del proyecto académico **SaaS Vital (Plataforma SaaS Gestión Hospitalaria)**.

> Proyecto académico multitenant de dos desarrolladores y ocho semanas, exclusivamente con datos sintéticos. No es un sistema hospitalario completo ni un producto certificado para expedientes reales.

## Propósito y estado actual

Clinical es propietario de los pacientes de cada clínica, expedientes, asignaciones profesionales y su historial, atenciones, notas, historia clínica común, preparaciones preliminares, mediciones e indicaciones vinculadas a la atención conforme al alcance que se autorice incrementalmente.

- **Estado actual (`CLINICAL-01A`):** base técnica verificable en **.NET 10 / ASP.NET Core**, compilable en `Release`, con endpoints técnicos `/health/live` y `/health/ready`, pruebas HTTP reales en memoria, verificación automatizada de fronteras entre capas, `Dockerfile` multietapa no root y pipeline completo en [`.github/workflows/ci.yml`](.github/workflows/ci.yml) (`Build and test (.NET 10)`, `Docker build verification` y `Publish image to GHCR` restringido a `push` en `main`).
- **Sin lógica de negocio todavía:** no contiene entidades clínicas, persistencia, migraciones, autenticación JWT, gRPC ni contratos implementados.

## Arquitectura interna

Dirección aprobada en **CLINICAL-00** (ver [`ADR-0001`](docs/adr/ADR-0001-clinical-architecture.md) y [`GEMINI.md`](GEMINI.md)): **Clean Architecture ligera**, organización preparada para **Vertical Slices** (`Features/` cuando existan casos de uso reales) y **modelo de dominio explícito/selectivo**.

```text
Saas.Clinical.sln
src/
  Clinical.Domain/          # Sin dependencias de otros proyectos ni frameworks externos
  Clinical.Application/     # Depende únicamente de Clinical.Domain
  Clinical.Infrastructure/  # Depende de Clinical.Application y Clinical.Domain
  Clinical.Api/             # Depende de Clinical.Application y Clinical.Infrastructure (composición)
tests/
  Clinical.IntegrationTests/
```

- **Decisión sobre `Clinical.UnitTests`:** se pospone la creación de `Clinical.UnitTests` hasta el primer incremento con reglas de dominio o aplicación reales, evitando pruebas tautológicas o clases artificiales únicamente para poblar un proyecto vacío.

## Requisitos técnicos

- **.NET 10 SDK** (`10.0.100` o superior en la banda `10.0.x`, fijado mediante [`global.json`](global.json)).
- **Docker** (opcional localmente para construir/verificar la imagen de contenedor y obligatorio en entornos sin .NET 10 SDK local).

## Compilación y pruebas

```bash
dotnet restore Saas.Clinical.sln
dotnet build Saas.Clinical.sln --configuration Release --no-restore
dotnet test Saas.Clinical.sln --configuration Release --no-build --verbosity normal
```

Endpoints técnicos en ejecución local (`http://localhost:8080`):

| Ruta | Método | Respuesta | Alcance |
|---|---|---|---|
| `/health/live` | `GET` | `200 OK` (`application/json`) | `scope: "process-only"` — proceso en ejecución. |
| `/health/ready` | `GET` | `200 OK` (`application/json`) | `scope: "process-only"` — disponibilidad básica de la API técnica. |

Un estado `Healthy` en `CLINICAL-01A` **únicamente** demuestra que el proceso y su API técnica están disponibles; **no** implica conectividad a PostgreSQL ni que un tenant esté preparado, aprovisionado o habilitado para operaciones clínicas.

## Ejecución mediante Docker

Imagen multietapa (`mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`), puerto no privilegiado `8080` y usuario no root (`$APP_UID`):

```bash
docker build --file Dockerfile --tag saas-clinical:local .
docker run --rm -p 8080:8080 saas-clinical:local
```

## CI/CD, alcance y exclusiones

El workflow [`.github/workflows/ci.yml`](.github/workflows/ci.yml) separa estrictamente validación y publicación:

- En PR (`develop` / `main`) y `push` a `develop`: ejecuta `Build and test (.NET 10)` y `Docker build verification` con permisos `contents: read` (sin publicar imágenes).
- En `push` a `main`: ejecuta además `Publish image to GHCR` (`packages: write`) cargando la misma imagen validada, etiquetada con `${GITHUB_SHA}` (sin `latest`) y registrando su digest inmutable en `GITHUB_STEP_SUMMARY`.
- La promoción declarativa hacia K3s/Argo CD pertenece exclusivamente a `saas-infra` (ver [`docs/engineering/GIT-CI-CD.md`](docs/engineering/GIT-CI-CD.md)).

En `CLINICAL-01A` quedan excluidos intencionalmente: entidades clínicas (pacientes, expedientes, asignaciones, consultas), EF Core/Npgsql/DbContext/migraciones, resolución multitenant, autenticación JWT/autorización clínica, gRPC, `EnsureClinicalTenantReady` e integraciones con Platform/Identity, MediatR, Redis/RabbitMQ, Saga/Outbox/Event Sourcing, CQRS distribuido, rulesets de GitHub y manifiestos Kubernetes/Helm/Argo CD.

- Reglas persistentes: [`GEMINI.md`](GEMINI.md)
- Decisión arquitectónica: [`docs/adr/ADR-0001-clinical-architecture.md`](docs/adr/ADR-0001-clinical-architecture.md)
- Convenciones Git y CI/CD: [`docs/engineering/GIT-CI-CD.md`](docs/engineering/GIT-CI-CD.md)
