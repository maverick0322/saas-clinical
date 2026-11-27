# Convenciones Git y CI/CD — Clinical

## Flujo de ramas adoptado

Clinical adopta las convenciones verificadas en `saas-platform`:

- `main`: versión promovida y candidata a publicación en GHCR y despliegue declarativo.
- `develop`: rama de integración y validación continua.
- `feature/*`: ramas de trabajo creadas desde `develop`.

Flujo previsto:

`feature/*` → PR → `develop` → validación → PR → `main` → GHCR → actualización declarativa por digest en `saas-infra` → Argo CD → K3s.

- **Sincronización posterior a promociones (`main` → `develop`):** tras promover `develop` → `main` mediante *merge commit*, sincronizar `develop` incorporando `origin/main` mediante **merge commit** (no `squash`) para preservar la ascendencia y evitar divergencias.
- **Convención de commits:** `feat:`, `fix:`, `docs:`, `test:`, `ci:`, `chore:`, `refactor:`.
- **Prohibiciones:** sin `push` directo a ramas protegidas, `force push`, borrado de ramas protegidas, mezcla no autorizada ni modificación de otros repositorios desde este flujo.

## Pipeline CI/CD definido en `.github/workflows/ci.yml`

El workflow [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) define los disparadores `pull_request` (`develop`, `main`) y `push` (`develop`, `main`), con concurrencia por rama/workflow (`cancel-in-progress` únicamente en PR):

| Job | Nombre de check | Condición | Permisos | Alcance |
|---|---|---|---|---|
| `build-and-test` | `Build and test (.NET 10)` | Siempre | `contents: read` | Verifica compatibilidad del SDK con `global.json` (`dotnet --version`), restaura, compila en `Release` y ejecuta pruebas sobre `Saas.Clinical.sln`. |
| `docker-build` | `Docker build verification` | `needs: build-and-test` | `contents: read` | Construye `ghcr.io/maverick0322/saas-clinical:${GITHUB_SHA}` con metadatos OCI, valida `/health/live`, `/health/ready` y usuario no root; en `push` a `main` guarda la imagen como artefacto temporal (`retention-days: 1`). |
| `publish-ghcr` | `Publish image to GHCR` | `needs: [build-and-test, docker-build]` y `push` a `refs/heads/main` | `packages: write` | Descarga y carga la **misma** imagen validada (sin reconstruir), inicia sesión en `ghcr.io` con `GITHUB_TOKEN`, publica `:${GITHUB_SHA}` (sin `latest`), registra referencia, SHA y digest inmutable en `GITHUB_STEP_SUMMARY` y cierra sesión. |

Principios de seguridad y compatibilidad GitOps (`saas-infra`):

- Acciones oficiales fijadas por SHA completo (`actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact`, `actions/download-artifact`) con `persist-credentials: false`.
- Permisos mínimos (`contents: read` en validación; `packages: write` aislado al job `publish-ghcr` en `main`).
- Sin `pull_request_target`, sin secretos en PR, sin PAT, sin `kubeconfig` ni tokens de Argo CD.
- Imagen compatible con el patrón GitOps validado en `saas-infra` (`runAsNonRoot`, puerto `8080`, sondas `/health/live` y `/health/ready`, despliegue por digest `@sha256:...`).

## Diferenciación de estado (CLINICAL-01A)

| Elemento | Estado actual | Observaciones |
|---|---|---|
| Convenciones de ramas, `.editorconfig`, `.gitattributes`, `.gitignore` y workflow `.github/workflows/ci.yml` | **Configurado en el repositorio** | Incluye los 3 jobs (`Build and test (.NET 10)`, `Docker build verification`, `Publish image to GHCR`). |
| CI remoto en PR hacia `develop` (`build-and-test`, `docker-build`, `publish-ghcr` omitido) | **Verificable en GitHub Actions al abrir el PR** | La evidencia real de ejecución se consulta tras publicar la rama y abrir el PR hacia `develop`. |
| Publicación en GHCR (`Publish image to GHCR`) | **Configurada, no ejecutada todavía** | Solo se ejecuta tras promover e integrar en `main`; nunca en ramas `feature/*` ni `develop`. |
| Rulesets / protección de ramas en GitHub (`develop` y `main`) | **No activado todavía** | Se configurarán cuando se autorice, enlazando únicamente los checks reales de CI. |
| Integración PostgreSQL / Testcontainers en CI | **No aplicable todavía** | Se incorporará junto con los incrementos autorizados de persistencia EF Core. |
| Despliegue declarativo en `saas-infra` / Argo CD / K3s | **Fuera de alcance** | Se gestiona exclusivamente mediante PR en `saas-infra` referenciando el digest inmutable publicado en GHCR. |
