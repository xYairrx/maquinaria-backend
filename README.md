# Maquinaria — Backend

API del **Sistema Integral de Operación y Rentabilidad de Activos**: un SaaS multi-tenant para empresas mexicanas de renta de maquinaria pesada, que cubre el ciclo completo del activo — cotización, renta, disponibilidad, mantenimiento, horómetros y rentabilidad por equipo.

Repo hermano del frontend: [`maquinaria-frontend`](https://github.com/xYairrx/maquinaria-frontend) (Angular).

> **Estado: solo andamiaje.** Compila y corre, pero todavía no hay entidades ni endpoints de negocio. Ver [estado y pendientes](docs/guias/estado-y-pendientes.md).

## Stack

| Capa | Tecnología | Alojamiento |
|---|---|---|
| API | .NET 10 (LTS), Minimal APIs | Railway (contenedor Docker) |
| ORM | EF Core 10 + Npgsql, Code-First | — |
| Base de datos | PostgreSQL 18 | Neon (gestionado) |
| Archivos | S3-compatible vía `IAlmacenamientoArchivos` | Cloudflare R2 |
| Auth | JWT propio + refresh token rotativo | — |
| Pruebas | xUnit | — |

El dominio se nombra **en español**: `Equipo`, `Renta`, `Cotizacion`, `Horometro`.

## Requisitos

| Herramienta | Versión | Verificar con |
|---|---|---|
| SDK de .NET | 10.0.302 | `dotnet --list-sdks` |
| `dotnet-ef` | 10.0.11 | `dotnet ef --version` |
| Git | 2.52+ | `git --version` |

No hace falta Docker ni PostgreSQL local: el desarrollo corre contra una rama de Neon, y Railway construye la imagen en sus servidores.

## Arrancar

```bash
git clone https://github.com/xYairrx/maquinaria-backend.git
cd maquinaria-backend
dotnet tool install --global dotnet-ef
```

Carga las dos cadenas de conexión — **es el único paso obligatorio antes de compilar**, y los detalles importan: ver [configuración](docs/guias/configuracion.md).

```bash
dotnet build Maquinaria.slnx
dotnet run --project src/Maquinaria.Api
```

La API queda en `http://localhost:5123` y `https://localhost:7020`. En `Development` el documento OpenAPI se expone en **`/openapi/v1.json`**, que es la fuente del cliente HTTP generado del frontend.

## Comandos

| Qué | Comando |
|---|---|
| Compilar | `dotnet build Maquinaria.slnx` |
| Pruebas | `dotnet test Maquinaria.slnx` |
| Arrancar la API | `dotnet run --project src/Maquinaria.Api` |
| Auditar dependencias | `dotnet list package --vulnerable --include-transitive` |
| Aplicar migraciones | `dotnet ef database update --context <Contexto> --project src/Maquinaria.Infraestructura --startup-project src/Maquinaria.Api` |

## Estructura

```
maquinaria-backend/
├── Maquinaria.slnx                 # formato .NET 10, no .sln
├── Directory.Packages.props        # versiones de paquetes centralizadas
├── docs/                           # diseño (00-05) y guías operativas
├── src/
│   ├── Maquinaria.Dominio/         # entidades, enums, reglas puras. Sin dependencias.
│   ├── Maquinaria.Aplicacion/      # casos de uso por módulo, DTOs, validaciones, interfaces
│   ├── Maquinaria.Infraestructura/ # EF Core, Npgsql, almacenamiento, JWT
│   └── Maquinaria.Api/             # endpoints minimal API, DI, middleware de tenant
└── tests/
    ├── Maquinaria.Dominio.Tests/
    └── Maquinaria.Api.Tests/       # integración contra Postgres real
```

Dirección de dependencias, estricta:

```
Api  →  Infraestructura  →  Aplicacion  →  Dominio
```

No existe la referencia `Api → Dominio`, y es deliberado. **EF Core vive solo en `Infraestructura`**: ni `Dominio` ni `Aplicacion` la conocen.

## Arquitectura en una línea

Monolito modular con **una base de datos por empresa**, todas en el mismo proyecto de Neon. Dos `DbContext`: `ContextoCentral` con cadena fija, y `ContextoEmpresa` construido por petición con la cadena de la empresa.

Consecuencia principal: el aislamiento es físico, así que **ninguna tabla de negocio lleva `tenant_id`** — y el precio es que las migraciones se corren N veces, una por base. El detalle está en [`01-arquitectura.md`](docs/01-arquitectura.md) y sus consecuencias prácticas en [convenciones](docs/guias/convenciones.md).

## Guías

| Guía | Para qué |
|---|---|
| [Configuración](docs/guias/configuracion.md) | Las dos cadenas de conexión, user secrets, Neon. **Empieza aquí.** |
| [Convenciones y reglas duras](docs/guias/convenciones.md) | Nombres, tipos, permisos, y las cinco invariantes del proyecto |
| [Estado y pendientes](docs/guias/estado-y-pendientes.md) | Qué está hecho, qué falta, decisiones abiertas |
| [Trampas conocidas](docs/guias/trampas-conocidas.md) | Errores ya pagados. Revísala antes de perder una tarde. |

## Documentación de diseño

El diseño completo vive en [`docs/`](docs/) y es la fuente de la verdad sobre el dominio:

| Documento | Contenido |
|---|---|
| [`README.md`](docs/README.md) | Resumen e invariantes del proyecto |
| [`00-puesta-en-marcha.md`](docs/00-puesta-en-marcha.md) | Herramientas, versiones, Neon, problemas y soluciones |
| [`01-arquitectura.md`](docs/01-arquitectura.md) | Stack, multi-tenancy, capas, auth, despliegue, contrato con el frontend |
| [`02-modelo-datos.md`](docs/02-modelo-datos.md) | Las ~75 entidades de los 26 módulos y sus reglas |
| [`especificacion-funcional.md`](docs/especificacion-funcional.md) | El texto de la especificación del negocio, extraído del `.docx` |
| [`03-plan-desarrollo.md`](docs/03-plan-desarrollo.md) | Fases y orden de tareas |
| [`04-pendientes.md`](docs/04-pendientes.md) | Decisiones abiertas y riesgos |
| [`05-esquema-fase0.md`](docs/05-esquema-fase0.md) | DDL de las 19 tablas de Fase 0, aprovisionamiento, login, índices |

> Los documentos son **especificación de diseño, no inventario**. Su checklist de avance ha estado por delante del código más de una vez; verifica siempre contra el repo.

## Contrato con el frontend

El frontend genera y **commitea** su cliente HTTP desde `/openapi/v1.json`, así que compila sin necesitar el backend corriendo, y un cambio de contrato se vuelve visible en un diff.

La evolución sigue **expandir → migrar → contraer**: el backend agrega lo nuevo conservando lo viejo, el frontend regenera y adopta, y solo entonces el backend elimina lo viejo. Cada despliegue debe ser compatible con la versión actualmente desplegada del otro repo.
