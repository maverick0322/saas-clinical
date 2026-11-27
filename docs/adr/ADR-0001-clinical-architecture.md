# ADR-0001 — Arquitectura interna de `saas-clinical` (CLINICAL-00)

- **Fecha:** 2026-11-26
- **Estado:** aprobada (`CLINICAL-00`, dictamen `COMPATIBLE CON PUNTUALIZACIONES`)
- **Ámbito:** arquitectura interna de `saas-clinical`, fronteras de servicio y restricciones operativas iniciales

## Contexto

Clinical pertenece a **SaaS Vital**, proyecto académico multitenant para clínicas de dos desarrolladores y ocho semanas, con datos exclusivamente sintéticos y backend en **.NET 10 / ASP.NET Core**. Requiere mantener desacoplamiento de reglas clínicas frente a infraestructura y transporte, evolucionar de forma incremental sin sobreingeniería y alinearse con las decisiones vigentes de Platform (`ADR-0002`, `ADR-0003`, `PLAT-05A`/`PLAT-05B`) y de Identity (`IDENTITY-SEC-01`).

## Decisión

Adoptar **Clean Architecture ligera**, organización de operaciones por funcionalidad (**Vertical Slices** dentro de `Clinical.Application` cuando existan casos de uso reales) y **modelo de dominio explícito/selectivo**:

- **`Clinical.Domain`:** entidades, invariantes, estados y comportamiento de negocio puro. Sin dependencias a otras capas, ASP.NET Core, EF Core, Npgsql, Kubernetes, JWT ni clientes externos.
- **`Clinical.Application`:** casos de uso organizados por funcionalidad (`Features/`), coordinación de autorización, transacciones y abstracciones justificadas. Depende únicamente de `Clinical.Domain`.
- **`Clinical.Infrastructure`:** persistencia privada, migraciones y adaptadores externos. Depende de `Clinical.Application` y `Clinical.Domain`.
- **`Clinical.Api`:** transporte HTTP, validación de entrada, autenticación, manejo de errores y composición de dependencias. Referencia `Clinical.Application` y, exclusivamente como raíz de composición, `Clinical.Infrastructure`.

## Siete puntualizaciones obligatorias de CLINICAL-00

1. **Estructura ligera sin ritual ni agregados monolíticos:** no rellenar los proyectos con clases base, repositorios genéricos, DTOs o interfaces artificiales; no crear un agregado `ClinicalRecord` que cargue todo el historial para modificar una parte.
2. **Integridad clínica y separación administrativo–clínica:** distinguir paciente y expediente (alta conjunta atómica y como máximo un responsable principal vigente con garantías transaccionales y de concurrencia); permitir expediente sin responsable inicial; mantener ciclos independientes de cita, atención y nota; restringir borradores a su autor y notas cerradas sin edición libre; exigir revisión profesional explícita (`REVISADA`) en preparaciones; no inferir diagnósticos del auxiliar; preservar registros históricos ante suspensión o desactivación y separar la ficha administrativa del espacio clínico.
3. **Persistencia multitenant vigente:** topología aprobada de una instancia PostgreSQL 17 dedicada por tenant gestionada por Platform mediante CloudNativePG, con base lógica privada `clinical` y rol propietario independiente `clinical_owner` sin privilegios globales. Consumo candidato mediante archivos `{tenantId:D}.connection` en un directorio de solo lectura (montaje sin `subPath`), con resolver *fail-closed* sin fallbacks ni registro de DSN/secretos, preservando `SslMode=Require` (dejando constancia de que la verificación de CA no fue certificada en `PLAT-05B` y requiere tratamiento posterior) y aplicando EF Core + PostgreSQL real sin migraciones automáticas indiscriminadas al arranque.
4. **Preparación interna Platform–Clinical:** `EnsureClinicalTenantReady` es un nombre conceptual y **no** un contrato ratificado. Antes de implementarlo deben acordarse transporte, autenticación servicio a servicio, DTO, errores, evidencia de esquema, concurrencia e idempotencia. Debe poder ejecutarse en estado `PENDIENTE_ADMIN` sin intercambiar DSN/contraseñas, sin crear datos clínicos ficticios, sin habilitar operaciones clínicas ordinarias y sin decidir la activación de la clínica.
5. **Seguridad y autorización *fail-closed*:** validación por objeto (identidad, tenant, rol/facultad, estado operativo, vínculo con expediente, acción y autoría) sin confiar en UUID o encabezados del cliente. Consumir `IDENTITY-SEC-01` (`RS256`, clave pública RSA PEM >= 2048 bits, `iss=saas-identity`, `aud=vital-saas`, `ClockSkew=30s`, `role`, `sub=accountId`, `tenant_id` obligatorio en roles clínicos; `SUPER_ADMIN` sin acceso clínico y `ADMIN_CLINICA` sin lectura clínica general). La ventana de aceptación local tras revocación de sesión (~15 min 30 s) requiere conciliación previa a habilitar operaciones dependientes. No elegir unilateralmente HTTP o gRPC para Platform–Clinical.
6. **Fronteras estrictas entre microservicios:** no compartir tablas, entidades, `DbContext`, claves foráneas ni consultas SQL con Platform, Identity, Agenda, Document, Prescription, Billing o BI. Conservar en su estado de conciliación los pendientes (`PC-01` a `PC-05`, `CU-24` y la relación organizacional auxiliar–especialista).
7. **Evolución incremental y exclusión de complejidad prematura:** pruebas con xUnit v3 proporcionales al riesgo (posponiendo `Clinical.UnitTests` mientras no existan reglas unitarias reales), health checks nativos diferenciando liveness y readiness (`process-only` en la base técnica) y exclusión explícita de MediatR, Unit of Work adicional sobre EF, Event Sourcing, CQRS con almacenamiento separado, RabbitMQ, Redis, Saga y Outbox mientras no exista necesidad demostrada y aprobada.

## Alternativas consideradas

- **Proyecto único VSA sin separación física:** menor fricción inicial, pero menor aislamiento explícito del dominio clínico y mayor riesgo de acoplar EF Core o ASP.NET Core en reglas clínicas.
- **Clean Architecture estricta con capas completas desde el día 1:** añadiría interfaces, mapeadores y proyectos de pruebas vacíos sin comportamiento verificable, contraviniendo KISS/YAGNI.

## Consecuencias y control

- **Beneficio:** las fronteras entre proyectos se verifican automáticamente mediante pruebas de arquitectura sobre `.csproj` y ensamblados compilados desde `CLINICAL-01A`.
- **Control:** cada incremento funcional futuro debe partir de casos de uso y contratos ratificados sin introducir dependencias no justificadas.
