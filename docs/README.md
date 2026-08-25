# Documentación — Maquinaria

Sistema Integral de Operación y Rentabilidad de Activos.
SaaS multi-tenant para empresas de renta de maquinaria y equipo.

---

## Índice

| Documento | Qué contiene | Cuándo leerlo |
|---|---|---|
| [00-puesta-en-marcha.md](00-puesta-en-marcha.md) | Herramientas, versiones, Neon, comandos ejecutados, problemas y soluciones. **§8: arrancar en otra máquina** | Al montar el entorno, al reconstruirlo o al clonar en una máquina nueva |
| [01-arquitectura.md](01-arquitectura.md) | Stack, multi-tenancy con base por empresa, capas, convenciones de BD, permisos, despliegue | Antes de tomar cualquier decisión técnica |
| [02-modelo-datos.md](02-modelo-datos.md) | ~75 entidades de los 26 módulos, decisiones estructurales, dónde vive cada regla | Antes de agregar una entidad o tabla |
| [03-plan-desarrollo.md](03-plan-desarrollo.md) | Método de rebanadas verticales, fases 0 a 6, desglose de arranque | Al planear qué sigue |
| [04-pendientes.md](04-pendientes.md) | Huecos de la especificación, decisiones de producto, riesgos técnicos | Antes de comprometer una fecha o cerrar una fase |
| [05-esquema-fase0.md](05-esquema-fase0.md) | DDL de las 19 tablas de la Fase 0, aprovisionamiento, login, índices, migraciones | Al escribir las entidades y migraciones de la Fase 0 |
| [06-alcance-fase1.md](06-alcance-fase1.md) | **El alcance del primer entregable.** Qué entra, qué no, las decisiones con su historial, y el inventario de 28 tablas | **Antes de tocar cualquier cosa de la Fase 1** |
| [06-esquema-fase1.sql](06-esquema-fase1.sql) | **El DDL completo de las 28 tablas**, con sus `CHECK`, `EXCLUDE`, columnas generadas y triggers. Se lee de corrido | Al escribir una entidad o una migración de la Fase 1 |
| [pruebas-esquema-fase1.sql](pruebas-esquema-fase1.sql) | **30 pruebas de las garantías** contra una base real: traslape de rentas, bodega/sucursal/patio, contrato inmutable. Corre en una transacción y hace `ROLLBACK` | Después de tocar una restricción, un trigger o una columna generada |

La especificación funcional original del negocio **ya está en el repositorio**:
[`../Especificacion_Funcional_Software_Renta_Maquinaria.docx`](../Especificacion_Funcional_Software_Renta_Maquinaria.docx),
y su texto extraído —legible y buscable sin abrir Word— en
[`especificacion-funcional.md`](especificacion-funcional.md).

> **Define 26 módulos, no 30.** La numeración llega a 30 pero M21, M22, M23 y M28 no existen.

---

## Lo mínimo que hay que saber

Si solo vas a leer una cosa de aquí, que sea esto.

**Es un producto SaaS, no un sistema para una empresa.** La especificación funcional está escrita como si fuera para una sola empresa. Al leerla, "la empresa" significa **el tenant**. La especificación no contempla las entidades de plataforma (Tenant, Plan, Suscripción, límites, superadmin, alta de clientes); esas se diseñaron aparte.

**Cada empresa tiene su propia base de datos.** Hay una base central con el catálogo de empresas y sus contratos, y una base por cliente creada al darlo de alta. Toda tabla de negocio vive en la base de la empresa y **no lleva `tenant_id`**. Ver `05-esquema-fase0.md` §1.

**Nada de DDL manual en la base de datos.** Todo cambio de esquema pasa por una migración de EF Core versionada, extensiones incluidas. Un cambio hecho a mano funciona en tu base y falta en las demás.

**Las migraciones nunca se aplastan ni se reescriben** después de un release. Una empresa puede estar dos versiones atrás y tiene que poder alcanzar. El historial es *append-only* para siempre.

**Cinco reglas de negocio las garantiza PostgreSQL, no el código.** La más importante: un equipo no puede tener dos rentas traslapadas, y eso lo impone un constraint `EXCLUDE USING gist` sobre `tstzrange`. Validarlo con un `if` en C# falla bajo concurrencia. Ver `02-modelo-datos.md` §2.1 y §4.

**No hay registro público.** Los tenants los da de alta un superadministrador; los usuarios se crean por invitación con token de un solo uso. Ver `05-esquema-fase0.md` §4.1.

**Dos repositorios significa que no hay commit atómico.** Los cambios de contrato de la API van en tres pasos: expandir → migrar → contraer. Ver `01-arquitectura.md` §10.6.

---

## Convenciones

| Tema | Regla |
|---|---|
| Nombres | Dominio en español (`Equipo`, `Renta`, `Cotizacion`) |
| Base de datos | `snake_case`; C#: `PascalCase` |
| Llaves primarias | `uuid` v7 (`Guid.CreateVersion7()`) |
| Dinero | `numeric(18,4)` — nunca `float` |
| Fechas | `timestamptz`, siempre UTC; zona horaria de presentación por tenant |
| Rangos de fecha | `tstzrange` |
| Borrado | Lógico (`eliminado_en`) en entidades de negocio |
| Terminal | PowerShell |
| Solución | `Maquinaria.slnx` (formato .NET 10, no `.sln`) |

---

## Mantenimiento de estos documentos

Son documentos **vivos**. Cuando una decisión cambie, se actualiza el documento en el mismo commit que el código — no después. Un documento que miente es peor que no tenerlo.

`04-pendientes.md` es el que más se mueve: cada vez que el negocio responde una duda, la respuesta baja al documento que corresponda y el pendiente se tacha.
