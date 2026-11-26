# GEMINI.md — saas-clinical

## 1. Contexto, autoridad y alcance

Repositorio previsto: `maverick0322/saas-clinical`. Clinical pertenece a **SaaS Vital (Plataforma SaaS Gestión Hospitalaria)**, proyecto académico multitenant para clínicas, de dos desarrolladores y ocho semanas, exclusivamente con datos sintéticos. No es un sistema hospitalario completo ni un producto certificado para expedientes reales.

Backend confirmado: .NET 10 / ASP.NET Core. Dirección aprobada en CLINICAL-00: **Clean Architecture ligera, operaciones organizadas por funcionalidad (Vertical Slices) y modelo de dominio explícito/selectivo**. No copiar automáticamente la arquitectura interna de Platform ni Identity.

Antes de modificar código, leer este archivo, el README, ADR, contratos y alcance del incremento autorizado que existan en el repositorio. Distinguir acuerdos confirmados, propuestas, pendientes y evidencia de ejecución. Los documentos candidatos no autorizan implementar contratos o reglas de negocio. Si una fuente histórica contradice una precisión posterior aprobada, conservar la trazabilidad, señalar la diferencia y aplicar la decisión vigente; si existe duda material, solicitar decisión antes de implementar la parte afectada.

Este archivo establece reglas persistentes, no autoriza por sí mismo ningún incremento. Los estados de ejecución, SHA, recursos del laboratorio y resultados de pruebas se conservan en documentación de seguimiento, no como constantes del código ni como instrucciones permanentes.

## 2. Fronteras del microservicio

Clinical es propietario de los pacientes de cada clínica, expedientes, asignaciones profesionales y su historial, atenciones, notas, historia clínica común, preparaciones preliminares, mediciones e indicaciones vinculadas a la atención, conforme al alcance funcional ratificado. Posee las reglas de acceso a sus recursos y su persistencia y migraciones privadas. El historial clínico se consulta con autorización; cualquier integración con otros servicios exige un contrato acordado.

- Platform posee el catálogo global de clínicas, su estado operativo y la coordinación del aprovisionamiento. Clinical no activa, suspende ni modifica tenants en Platform.
- Identity posee cuentas, credenciales, autenticación, sesiones, roles y permisos organizacionales. Las cuentas son independientes por clínica. Clinical no administra contraseñas, emite tokens ni mantiene una identidad global de personas.
- Agenda posee citas y planificación; una cita no implica atención, nota ni autorización clínica.
- Document, Prescription, Billing y BI se integran conforme a sus límites y contratos ratificados. Clinical no se convierte en autorizador universal, propietario de los archivos/recetas/cobros de otros servicios ni orquestador general de la plataforma. Cada servicio protege sus propios recursos.
- La relación organizacional auxiliar–especialista y su contrato con Clinical requieren ratificación. No duplicar datos de Identity por defecto ni inferir acceso de una cita.
- No compartir tablas, entidades de dominio, DbContext, claves foráneas ni consultas SQL entre servicios. Las referencias externas son identificadores y contratos explícitos, no navegaciones EF entre dominios.

No implementar por anticipado portales, formularios por especialidad, hospitalización, órdenes formales de estudios, gráficas clínicas descartadas (CU-09), cobros o funciones fuera del incremento aprobado.

## 3. Arquitectura interna y dependencias

Estructura inicial candidata aceptada en CLINICAL-00:

```text
src/
  Clinical.Api/
  Clinical.Application/
  Clinical.Domain/
  Clinical.Infrastructure/
tests/
  Clinical.UnitTests/
  Clinical.IntegrationTests/
```

- `Clinical.Domain`: entidades, invariantes, estados y comportamiento de negocio puro. No depende de otras capas, ASP.NET Core, EF Core, Npgsql, Kubernetes, JWT ni clientes externos. No crear un agregado `ClinicalRecord` que cargue todo el historial para modificar una parte.
- `Clinical.Application`: operaciones organizadas por funcionalidad, coordinación de autorización, reglas del dominio, transacciones y dependencias mediante abstracciones justificadas. Depende de Domain; no de Infrastructure ni del transporte. Separar comandos y consultas cuando ayude a la comprensión; no implica CQRS distribuido.
- `Clinical.Infrastructure`: implementación de persistencia, migraciones y adaptadores de integraciones; depende de Application y/o Domain. Los detalles de conexión multitenant y EF pertenecen aquí, no a las entidades clínicas.
- `Clinical.Api`: transporte, autenticación, serialización, validación de entrada, errores y composición de dependencias. Referencia Application y, como raíz de composición, Infrastructure. No contiene reglas clínicas ni acceso SQL de negocio. No imponer dependencias adicionales innecesarias.

La estructura no obliga a rellenar proyectos con clases artificiales ni a crear todos los módulos desde el inicio. Una operación sencilla no necesita múltiples interfaces, handlers, DTO y mapeadores por ritual. Centralizar invariantes compartidas y controles de autorización, manteniendo cohesión de cada funcionalidad. Agregados y límites transaccionales se justifican por reglas reales, no por relaciones entre tablas.

## 4. Reglas funcionales y de integridad

Estas reglas orientan el diseño; las precisiones aceptadas individualmente y los detalles de CU aún sujetos a ratificación no se implementan sin autorización del incremento correspondiente.

- Paciente y expediente son conceptos distintos del mismo tenant. El alta ordinaria conjunta debe ser atómica dentro de Clinical. Un expediente por paciente y como máximo un responsable principal vigente requieren garantías transaccionales y restricciones resistentes a concurrencia, no solo comprobaciones en memoria.
- Permitir un expediente sin responsable inicial cuando corresponda. Registrar un paciente no convierte al registrante en especialista tratante ni concede acceso clínico. Distinguir asignación inicial, reasignación del principal y vinculaciones adicionales. Preservar historia y autoría; retirar solo el fundamento de acceso revocado. No asumir actores o facultades pendientes.
- Cita, atención y nota son conceptos y ciclos de vida independientes. Una atención puede finalizar sin cita, sin pago y con nota todavía en borrador, según las reglas funcionales vigentes. El indicador de consultas realizadas se deriva de atenciones finalizadas, no de citas ni notas cerradas.
- Borradores de notas privados para su autor con autorización vigente; notas cerradas consultables por especialistas legitimados, sin edición libre ni transferencia silenciosa de autoría. Las reglas de corrección de registros cerrados requieren ratificación.
- La preparación puede existir antes de una atención. Asociarla a una atención **no** la marca como `REVISADA`: se exige revisión profesional efectiva, explícita, con autoría y trazabilidad. Una preparación revisada no admite edición libre. Las correcciones posteriores se sujetan a reglas ratificadas.
- Distinguir antecedentes declarados, mediciones preliminares y valoración profesional. El auxiliar no diagnostica, prescribe ni cierra notas profesionales.
- Suspender clínicas o desactivar cuentas no elimina automáticamente registros históricos. CRUD no equivale a borrado clínico definitivo.
- Separar la ficha administrativa del espacio clínico, incluso en las respuestas de API. No enviar notas, diagnósticos o historia clínica a actores que solo puedan gestionar datos administrativos.

No crear entidades, estados, cardinalidades, campos, permisos o endpoints por completar un inventario. Los PC-01 a PC-05, el identificador CU-24 y el proceso auxiliar excepcional conservan su estado de conciliación donde corresponda. No renumerar requisitos ni casos de uso por iniciativa propia.

## 5. Persistencia multitenant y preparación de clínicas

La topología aprobada y validada en infraestructura por Platform (ADR-0002, ADR-0003, PLAT-05A/05B) es **una instancia PostgreSQL dedicada por tenant, con bases lógicas privadas y propietarios independientes por servicio**. Los despliegues de aplicaciones son compartidos entre tenants; Platform conserva su persistencia global separada. PostgreSQL 17 es la versión de referencia del laboratorio.

- Platform coordina la creación de instancias mediante CloudNativePG y prepara bases, roles y distribución de credenciales. Clinical no administra Kubernetes, CNPG, roles PostgreSQL, namespaces ni Secrets del operador.
- Clinical posee sus esquemas y migraciones. No crear bases/usuarios con credenciales administrativas ni acceder a bases de Identity u otros servicios. No crear una base global de Clinical sin necesidad y aprobación explícitas.
- La distribución implementada en Platform utiliza Secrets independientes por servicio. Para Clinical el formato consumidor candidato es `{tenantId:D}.connection` dentro de un directorio de solo lectura; montaje sin `subPath` gestionado en `saas-infra`. El consumo real por Clinical todavía debe probarse. Evaluar compatibilidad con Identity sin compartir su código.
- El resolver debe seleccionar exclusivamente la conexión del tenant validado, rechazar contexto ausente, ambiguo o desconocido y evitar fallbacks a conexiones de otra clínica. No construir destinos con entradas arbitrarias ni confiar en un tenant de ruta/cuerpo sin contrastarlo con el contexto confiable. No registrar DSN, contraseñas ni contenido de Secrets. El nombre de opciones, política de actualización y diseño del resolver de Clinical se ratifican en el incremento correspondiente; no introducir watchers, cachés o servicios centrales sin necesidad.
- Mantener el requisito TLS de Platform (`SslMode=Require` en las conexiones distribuidas). No afirmar que equivale a validación de identidad del servidor: la verificación de CA no fue certificada en PLAT-05B y requiere tratamiento explícito posterior. No deshabilitar TLS por conveniencia en integración real.
- Usar EF Core + PostgreSQL cuando exista persistencia autorizada. Probar transacciones, restricciones e aislamiento real; no confiar exclusivamente en EF InMemory. No migrar todas las bases al arrancar cada réplica por defecto ni habilitar una API genérica de migraciones/SQL. El mecanismo de preparación y la evolución de esquemas de tenants existentes requieren alcance explícito. Separar lógicamente permisos de preparación y operaciones ordinarias cuando el diseño ratificado lo exija; no inventar roles de BD que contradigan el aprovisionamiento actual.

### Preparación interna Platform–Clinical

`EnsureClinicalTenantReady` es un nombre conceptual, **no un RPC, ruta o contrato ratificado**. Antes de implementar, acordar transporte, autenticación servicio a servicio, DTO, errores, evidencia de esquema, concurrencia y reintentos. La capacidad mínima propuesta identifica tenant y operación, resuelve internamente su conexión, aplica/comprueba migraciones propias y devuelve preparación verificable e idempotente, distinguiendo fallos transitorios de conflictos definitivos. No intercambia DSN o contraseñas, no crea pacientes ficticios para declarar readiness y no modifica el catálogo de Platform.

La preparación técnica autorizada debe poder ejecutarse con tenant `PENDIENTE_ADMIN`; no habilita operaciones clínicas ordinarias ni decide la activación. Platform activa únicamente cuando dispone de las evidencias obligatorias de infraestructura, Clinical e Identity. No sustituir este proceso con una activación manual, un bypass o una comprobación superficial de conectividad.

## 6. Seguridad y contratos

- Validar identidad, contexto de tenant, rol/facultad organizacional, estado operativo, vínculo con expediente, acción concreta y estado/autoría del recurso cuando corresponda. Un UUID, un JWT válido, un rol o el paso por Gateway no son autorización por objeto. Aplicar controles a lecturas, escrituras, búsquedas, listados, referencias, adjuntos e integraciones. SuperAdmin no tiene acceso clínico; AdminClinica no tiene lectura clínica general por su rol.
- Clinical verifica sus propios recursos; los demás servicios verifican los suyos. Cuando no pueda determinarse de manera confiable una autorización requerida, denegar (fail-closed), sin revelar existencia o datos de otra clínica. No implementar políticas de acceso temporal o claims inventados para desbloquear pruebas.
- Consumir el contrato `IDENTITY-SEC-01` vigente, verificándolo antes del incremento de autenticación. En la referencia revisada: RS256 únicamente, clave pública RSA PEM de al menos 2048 bits, issuer `saas-identity`, audience `vital-saas`, validación de firma y vigencia, `ClockSkew` de 30 s, sin mapeo automático de claims, `role` como claim de rol. `sub` representa `accountId`, `tenant_id` es obligatorio y no vacío para roles clínicos; `SUPER_ADMIN` no lleva tenant y no accede a recursos clínicos. Roles cerrados: `SUPER_ADMIN`, `ADMIN_CLINICA`, `ESPECIALISTA`, `AUXILIAR`. Rechazar combinaciones inválidas y no aceptar HS256/none ni recibir clave privada RSA o secreto HMAC de refresh tokens. No crear emisores ni login en Clinical.
- La validación local del JWT no acredita revocación inmediata ni vigencia actual de roles/tenant. El contrato de Identity revisado reconoce una ventana de aceptación de hasta 15 min 30 s con la configuración predeterminada tras revocar una sesión. Antes de habilitar operaciones dependientes, conciliar la política de frescura de cuenta, permisos, estado de tenant y revocación con Platform e Identity; no prometer revocación inmediata ni introducir Redis, listas negras o introspección sin acuerdo.
- No exponer operaciones internas a clientes públicos ni confiar en encabezados que suplanten actor/tenant. El mecanismo de autenticación interna y delegación del usuario debe estar ratificado; no reutilizar un secreto ajeno ni elegir HTTP/gRPC unilateralmente. El gRPC ya existente Platform–Identity no ratifica Platform–Clinical.
- Secretos fuera de Git, imágenes, artefactos, ejemplos y logs. Datos exclusivamente sintéticos. Auditoría de acciones relevantes con actor, tenant, fecha, recurso y resultado cuando corresponda, minimizando datos sensibles. No registrar contenido clínico, credenciales, tokens, parámetros sensibles ni trazas que los contengan.
- Mantener contratos explícitos y versionados (OpenAPI y/o Protobuf según acuerdo); cambios compatibles, DTO propios, errores estables y pruebas consumidor–proveedor. Las rutas, campos y códigos concretos dependen del contrato ratificado. Evitar exponer entidades EF o excepciones internas. Respetar la semántica temporal acordada (GMT-6 para el MVP donde aplique), sin decidir unilateralmente formato de almacenamiento y transporte.

## 7. Principios y convenciones de ingeniería

- KISS: la solución más simple que satisface el caso de uso y las garantías de seguridad e integridad. YAGNI: no implementar necesidades hipotéticas. SOLID: responsabilidades claras y abstracciones que resuelven acoplamiento real. DRY: evitar duplicación de conocimiento y reglas, no eliminar toda repetición incidental mediante genéricos prematuros.
- Código idiomático .NET 10, nombres expresivos, funciones acotadas, dependencias explícitas, asincronía para E/S real y CancellationToken cuando aplique. Preferir mecanismos nativos antes de nuevas dependencias; justificar cada paquete. Usar configuración estándar, nullable/análisis de compilación coherentes con la base técnica acordada y versiones reproducibles de SDK y paquetes.
- No imponer MediatR, repositorios genéricos, Unit of Work adicional sobre EF, motores de políticas dinámicas, DDD táctico completo, eventos de dominio, CQRS con bases separadas, Event Sourcing, RabbitMQ, Redis, Saga, Outbox, Vault, service mesh u operadores nuevos. Solo introducir un mecanismo adicional con necesidad demostrada y alcance aprobado; las transacciones ACID locales son la base para operaciones atómicas. No usar transacciones distribuidas para sustituir límites de ownership.
- Health checks nativos y logging estructurado, sin pila completa de observabilidad en la base técnica. Distinguir liveness de readiness; salud del proceso no acredita preparación de todos los tenants ni activación clínica. No hacer pasar una respuesta técnica de health por evidencia de persistencia.
- Contenedores con imagen oficial .NET 10, build multietapa, runtime no SDK, usuario no root, puerto no privilegiado y contexto limitado. Validar ejecución real. Reutilizar prácticas de Platform sin copiar el Worker ni sus permisos administrativos. No fijar puertos/rutas de negocio o configuración de infraestructura sin revisar acuerdos y evitar colisiones.

## 8. Pruebas y definición de terminado

Usar xUnit v3 como base técnica propuesta a partir de Platform. Cada incremento incorpora pruebas proporcionales al riesgo y no crea pruebas triviales para aparentar cobertura.

- Base técnica: restauración, compilación, pruebas HTTP reales del endpoint técnico en memoria y verificación de contenedor; no exigir PostgreSQL o Kubernetes para comprobar un proceso sin persistencia. Los proyectos de pruebas deben tener una finalidad real; no añadir tests tautológicos para llenar el proyecto unitario si aún no hay reglas de dominio.
- Dominio y aplicación: pruebas positivas y negativas de invariantes, transiciones, autoría, permisos y fallos. Probar cambios de fundamento de acceso, nota cerrada y revisión explícita cuando existan esas operaciones.
- Persistencia: PostgreSQL real/Testcontainers cuando se incorpore EF Core; rollback paciente–expediente, unicidad y concurrencia del principal vigente, repetición segura de preparación y aislamiento entre al menos dos tenants con recursos propios. Evitar que pruebas modifiquen bases del laboratorio sin autorización.
- Contratos: validar productor y consumidor contra versiones acordadas; probar autenticación interna, contexto inválido, timeouts, errores y reintentos según contrato. Probar rechazo de token/tenant incompatibles, rol insuficiente, usuario desactivado, suspensión, IDOR/BOLA, búsquedas y recursos fuera de ámbito; verificar limitaciones reales de revocación y no afirmar garantías no probadas.
- No declarar PASS por lectura estática. Registrar comandos, resultados y pruebas no ejecutadas, con distinción entre validación local, CI remoto, publicación GHCR, integración PostgreSQL y despliegue real. El éxito de un test aislado no certifica todo el sistema.

## 9. Git, CI/CD y límites operativos

Adoptar el flujo validado de Platform como referencia para la base técnica de Clinical, después de comprobar el repositorio real:

`feature/*` desde `develop` -> PR a `develop` -> validación -> PR a `main` -> GHCR -> PR declarativo por digest en `saas-infra` -> Argo CD -> K3s.

- No asumir que ramas, rulesets, nombres de checks o permisos ya existen en Clinical. Configurar protecciones cuando sea autorizado, solo con checks reales. No copiar ciegamente políticas de Platform; usar sus convenciones para mantener consistencia.
- Sin push directo a ramas protegidas, force push, borrado de ramas, merge, cambios de rulesets, publicación o despliegue sin solicitud expresa. No tocar otros repositorios. Tras promoción mediante merge commit, sincronizar `main` hacia `develop` mediante merge commit, no squash, conforme al flujo adoptado.
- Workflows de PR con permisos mínimos (`contents: read`), sin secretos ni escritura. `packages: write` limitado al job de publicación autorizado desde `main`. Validar la misma imagen que se publica; etiquetar por SHA completo y desplegar por digest inmutable, no `latest`. No exponer secretos en artefactos ni resultados de CI.
- Ansible, Kubernetes, CNPG y Argo CD se gestionan en el chat de infraestructura / `saas-infra`. Clinical no reanuda el Worker de Platform, cambia presupuestos de reintento ni modifica recursos conservados del laboratorio. No certificar despliegues por el mero éxito del build.

## 10. Protocolo obligatorio para Gemini

1. Inspeccionar estado Git, rama, archivos existentes, dependencias y cambios locales. No sobrescribir trabajo ajeno ni asumir la existencia de `develop`; ante repositorio inicial o inaccesible, informar y pedir decisión sobre la acción administrativa necesaria.
2. Confirmar el incremento solicitado, alcance, exclusiones, fuentes y criterios de aceptación. Identificar discrepancias o contratos pendientes; no extender el alcance sin permiso.
3. Implementar únicamente lo autorizado, en rama de trabajo conforme al flujo aprobado. Mantener cambios pequeños y funcionales; no crear toda la arquitectura de negocio, persistencia y seguridad por adelantado.
4. Ejecutar las verificaciones acordadas, inspeccionar el diff y comprobar límites de dependencias, aislamiento, secretos y necesidad de cada paquete.
5. Entregar lista exacta de archivos, resumen de cambios, comandos y resultados reales, riesgos, bloqueos, decisiones no tomadas y siguiente incremento sugerido. No presentar commits, PR, CI, publicación o despliegue como realizados sin evidencia.
6. Si una decisión pendiente bloquea una operación, detener esa parte y continuar solo trabajo independiente expresamente autorizado. No inventar contratos, permisos, excepciones, estados ni datos de prueba que oculten un defecto de seguridad.

La prioridad es materializar funcionalidades clínicas útiles y desbloquear la preparación real de clínicas, no desarrollar infraestructura preventiva. No reabrir CLINICAL-00 salvo incompatibilidad concreta demostrada. El primer esquema persistente debe derivarse de funcionalidad autorizada, no de un inventario especulativo; no esperar a terminar todo el dominio para integrar Platform.

## 11. Referencias y decisiones vigentes

Consultar las versiones exactas y verificar cambios posteriores antes de implementar dependencias. Estas referencias son la línea de partida, no una congelación unilateral de contratos externos.

- CLINICAL-00, dictamen `COMPATIBLE CON PUNTUALIZACIONES` y documento `SAAS_VITAL_CLINICAL_00_DIRECCION_APROBADA_CON_PUNTUALIZACIONES_v0.2.md`: siete precisiones obligatorias. Los cambios posteriores de topología y aprovisionamiento del handoff Platform → Clinical prevalecen sobre los pendientes históricos de topología.
- Catálogo funcional v0.4.2 y descripciones CU v0.1.1: candidatos a conciliación, no ratificación automática de detalles. Conservar los identificadores y precisar alcance de cada incremento.
- Platform, referencia `f7b8e5633e0b3ad34f18a9e8754c1e097fa3fb8b`: [GEMINI.md](https://github.com/maverick0322/saas-platform/blob/f7b8e5633e0b3ad34f18a9e8754c1e097fa3fb8b/GEMINI.md), [ADR](https://github.com/maverick0322/saas-platform/tree/f7b8e5633e0b3ad34f18a9e8754c1e097fa3fb8b/docs/adr), [GIT-CI-CD](https://github.com/maverick0322/saas-platform/blob/f7b8e5633e0b3ad34f18a9e8754c1e097fa3fb8b/docs/engineering/GIT-CI-CD.md), [PLAT-05B](https://github.com/maverick0322/saas-platform/blob/f7b8e5633e0b3ad34f18a9e8754c1e097fa3fb8b/docs/PLAT-05B-SERVICE-DATABASES.md), [PLAT-02B](https://github.com/maverick0322/saas-platform/blob/f7b8e5633e0b3ad34f18a9e8754c1e097fa3fb8b/docs/PLAT-02B-PROVISIONING-WORKER-DESIGN.md) (contrato Clinical candidato).
- Identity, referencia `a374584b445e7a09f55e4d9535f09439c4f65797`: [README](https://github.com/Eugene1805/saas-identity/blob/a374584b445e7a09f55e4d9535f09439c4f65797/README.md), [IDENTITY-SEC-01](https://github.com/Eugene1805/saas-identity/blob/a374584b445e7a09f55e4d9535f09439c4f65797/docs/IDENTITY-SEC-01-JWT-CONTRACT.md).
- Infraestructura: [INFRA-PLAT-05B](https://github.com/maverick0322/saas-infra/blob/571f06056f1e58dea0a58b50ef9f274793dbbdf1/docs/INFRA-PLAT-05B.md), referencia del handoff, no autorización para modificarla.
