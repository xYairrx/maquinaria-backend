# Puesta en marcha del entorno

> Cómo llegar de una máquina vacía a un backend que compila.
> Sirve como guía para reconstruir el entorno y como bitácora de lo que ya se hizo.
> Última actualización: 2026-08-19.

---

## 1. Herramientas y versiones

Verificadas el 2026-08-17 en la máquina de desarrollo:

| Herramienta | Versión | Cómo verificar |
|---|---|---|
| SDK de .NET | 10.0.302 (también 9.0.316) | `dotnet --list-sdks` |
| Node.js | v24.19.0 | `node -v` |
| npm | 11.17.0 | `npm -v` |
| Git | 2.55.0 | `git --version` |
| Angular CLI | 22.1.4 | `ng version` |
| `dotnet-ef` | 10.0.11 | `dotnet ef --version` |
| Visual Studio | 2026 | — |

**No se necesita** ni Docker ni PostgreSQL instalados localmente:

- **PostgreSQL** → se usa una rama de Neon como base de desarrollo. Ver §2.
- **Docker** → Railway construye la imagen del contenedor en sus servidores a partir del `Dockerfile`. No hace falta construirla en local.

---

## 2. Base de datos: Neon

### 2.1 Creación del proyecto

| Campo | Valor | Nota |
|---|---|---|
| Project name | `maquinaria` | |
| Postgres version | **18** | Trae `uuidv7()` nativo, útil para scripts de semilla |
| Region | *debe coincidir con la región de Railway* | **No se puede cambiar después.** Railway US East está en Virginia, así que lo correcto es `AWS US East 1 (N. Virginia)`, no Ohio |
| Neon Auth | **desactivado** | Ver §2.4 |

La región es el único campo irreversible de esa pantalla. Cada consulta de EF Core es un viaje de red: misma región son 1-2 ms, regiones distintas dentro de EE. UU. son ~15-60 ms. Con 20 consultas por pantalla, es la diferencia entre 40 ms y 1.2 s.

### 2.2 Rama de desarrollo

| Campo | Valor | Nota |
|---|---|---|
| Name | `dev` | |
| Auto-delete | **Never** | El valor por defecto es *After 1 day*, pensado para ramas efímeras de CI. Dejarlo así **borraría la rama de desarrollo al día siguiente** |
| Parent branch | `production` | La rama principal de Neon se llama `production`, no `main` |
| Tipo | Branch data and schema | |

### 2.3 Verificación de extensiones

En el SQL Editor, con la rama `dev` seleccionada:

```sql
SELECT name, default_version FROM pg_available_extensions
WHERE name IN ('btree_gist', 'pg_trgm');
```

Resultado obtenido: `btree_gist 1.8`, `pg_trgm 1.6`. ✅

`btree_gist` es **bloqueante**: de ella depende el constraint `EXCLUDE` que impide que un equipo tenga dos rentas traslapadas, que es la regla central del sistema (ver `02-modelo-datos.md` §2.1).

> **Solo se verificó la disponibilidad. No se ejecutó `CREATE EXTENSION`.**
> Regla del proyecto: **nada de DDL manual en la base de datos.** Todo cambio de esquema —extensiones incluidas— va en una migración de EF Core versionada en el repo. Un `CREATE EXTENSION` hecho a mano en `dev` funciona hoy y falla el día que se despliega a `production` o se crea una rama nueva.

### 2.4 Por qué Neon Auth quedó desactivado

Choca con dos decisiones ya tomadas:

1. **Cada empresa tiene su propia base de datos** y sus usuarios viven ahí. Neon Auth asume un directorio único de usuarios; nuestro modelo es justo lo contrario.
2. Necesitamos la matriz `rol × módulo × acción` con roles personalizables por empresa. Neon Auth da usuarios y sesiones, no autorización de grano fino.

Además crea tablas propias en la base, está orientado a frontends JS y ataría la capa de identidad a Neon — que hoy es "Postgres gestionado", intercambiable. Ver también `04-pendientes.md` §5.1 sobre el descarte del SSO.

### 2.5 Cadenas de conexión

De la rama `dev` se necesitan **las dos**, y no son intercambiables:

| Endpoint | Clave de configuración | Para qué |
|---|---|---|
| Con `-pooler` en el host | `ConnectionStrings:Central` | Runtime de la API. La cadena de cada empresa es esta misma con el nombre de base cambiado |
| Sin `-pooler` | `ConnectionStrings:Migraciones` | Migraciones **y** el `CREATE DATABASE` del aprovisionamiento |

El motivo está en `01-arquitectura.md` §10.1: el endpoint pooled corre PgBouncer en modo transacción, que no soporta DDL ni estado de sesión. Las migraciones y el `CREATE DATABASE` del aprovisionamiento van obligatoriamente por el endpoint directo.

**Todas las bases de empresa viven en el mismo proyecto de Neon**, así que la cadena de cada una es la central con el nombre de base cambiado.

**Nunca en `appsettings.json`.** En desarrollo van en *user secrets*; en producción, en variables de entorno de Railway:

```bash
dotnet user-secrets init --project src/Maquinaria.Api
```

```bash
dotnet user-secrets set "ConnectionStrings:Central" "<cadena pooled>" --project src/Maquinaria.Api
```

```bash
dotnet user-secrets set "ConnectionStrings:Migraciones" "<cadena directa>" --project src/Maquinaria.Api
```

Los user secrets se guardan en `%APPDATA%\Microsoft\UserSecrets\`, fuera de la carpeta del proyecto, así que no pueden acabar en el repositorio por accidente. `IConfiguration` lee de user secrets, variables de entorno y `appsettings.json` de forma transparente, con esa precedencia.

---

## 3. Estructura del repositorio

Dos repositorios independientes (ver `01-arquitectura.md` §10.6):

```
Documents/Maquinaria/          <- solo contenedor local, NO es un repo
├── maquinaria_back/           <- repo 1  → Railway
│   ├── .gitignore
│   ├── Maquinaria.slnx
│   ├── Directory.Packages.props
│   ├── docs/
│   ├── src/
│   │   ├── Maquinaria.Dominio
│   │   ├── Maquinaria.Aplicacion
│   │   ├── Maquinaria.Infraestructura
│   │   └── Maquinaria.Api
│   └── tests/
│       ├── Maquinaria.Dominio.Tests
│       └── Maquinaria.Api.Tests
└── maquinaria_front/          <- repo 2  → Cloudflare Pages
```

`Maquinaria` **no** debe inicializarse como repositorio: sería un repo conteniendo otros dos, y git se confunde con qué le pertenece a cada uno.

### Los cuatro proyectos y la dirección de las dependencias

```
Api  →  Infraestructura  →  Aplicacion  →  Dominio
```

| Proyecto | Plantilla | Contiene |
|---|---|---|
| `Maquinaria.Dominio` | `classlib` | Entidades, enums, reglas puras. **Sin dependencias** |
| `Maquinaria.Aplicacion` | `classlib` | Casos de uso por módulo, DTOs, interfaces |
| `Maquinaria.Infraestructura` | `classlib` | `DbContext`, Npgsql, R2, JWT — las implementaciones |
| `Maquinaria.Api` | `webapi` | Endpoints minimal API, DI, middleware |

Las flechas apuntan siempre **hacia adentro**. Que `Dominio` no dependa de nada es lo que permite probar las reglas de negocio sin base de datos, y es la razón de ser de esta separación.

Nótese que **no existe** la referencia `Api → Dominio`. Llega en cascada, pero al no declararla nada invita a usar una entidad del dominio directamente en un endpoint.

EF Core vive **solo en Infraestructura**. Ni `Dominio` ni `Aplicacion` la conocen.

---

## 4. Comandos ejecutados

Desde `Documents/Maquinaria`. Todo en **PowerShell** (no cmd: los comandos con `Get-ChildItem` son cmdlets que cmd no conoce).

### 4.1 Repositorio y documentación

```powershell
mkdir maquinaria_back
Move-Item docs, "Especificacion_Funcional_Software_Renta_Maquinaria (1) (1).docx" maquinaria_back
cd maquinaria_back
git init
dotnet new gitignore
```

`dotnet new gitignore` genera un `.gitignore` completo de .NET (`bin/`, `obj/`, `.vs/`, user secrets). Verificado: filtra correctamente los artefactos de compilación.

### 4.2 Solución y proyectos

```powershell
dotnet new sln -n Maquinaria

dotnet new classlib -o src/Maquinaria.Dominio         -f net10.0
dotnet new classlib -o src/Maquinaria.Aplicacion      -f net10.0
dotnet new classlib -o src/Maquinaria.Infraestructura -f net10.0
dotnet new webapi   -o src/Maquinaria.Api             -f net10.0

dotnet new xunit -o tests/Maquinaria.Dominio.Tests -f net10.0
dotnet new xunit -o tests/Maquinaria.Api.Tests     -f net10.0
```

`webapi` sin `--use-controllers` significa **minimal APIs**, que es lo decidido: un endpoint por línea, cada uno invocando un caso de uso de `Maquinaria.Aplicacion`.

### 4.3 Registro en la solución

```powershell
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName)
```

El `-ExpandProperty FullName` es indispensable — ver §6.2.

### 4.4 Referencias

```powershell
dotnet add src/Maquinaria.Aplicacion      reference src/Maquinaria.Dominio
dotnet add src/Maquinaria.Infraestructura reference src/Maquinaria.Aplicacion
dotnet add src/Maquinaria.Api             reference src/Maquinaria.Infraestructura

dotnet add tests/Maquinaria.Dominio.Tests reference src/Maquinaria.Dominio
dotnet add tests/Maquinaria.Api.Tests     reference src/Maquinaria.Api
```

### 4.5 Herramienta de migraciones

```powershell
dotnet tool install --global dotnet-ef
```

### 4.6 Paquetes

```powershell
dotnet add src/Maquinaria.Api package Microsoft.AspNetCore.OpenApi --version 10.0.11
```

Los de EF Core se agregaron editando el `.csproj` directamente, porque ya estaba activo *Central Package Management* (§5).

### 4.7 Verificación

```powershell
dotnet build Maquinaria.slnx
dotnet list package --vulnerable --include-transitive
```

El segundo comando es la auditoría de seguridad de dependencias de .NET. `--include-transitive` es la parte importante: sin él solo revisa lo que declaraste tú, no lo que arrastran tus paquetes. Conviene correrlo antes de cada despliegue.

---

## 5. Central Package Management

`Directory.Packages.props` en la raíz del repo concentra **todas** las versiones de paquetes. Los `.csproj` declaran qué paquete usan, nunca cuál versión:

```xml
<!-- antes -->
<PackageReference Include="xunit" Version="2.9.3" />

<!-- ahora -->
<PackageReference Include="xunit" />
```

Con seis proyectos y creciendo, evita que uno quede en una versión y otro en otra.

Está activo también `CentralPackageTransitivePinningEnabled`, que fija además las dependencias **transitivas**. Eso resolvió limpiamente el problema de §6.4: una vulnerabilidad en un paquete que no aparecía en ningún `.csproj`.

### Paquetes actuales

| Paquete | Versión | Proyecto |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | Infraestructura |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.11 | Infraestructura |
| `Microsoft.AspNetCore.OpenApi` | 10.0.11 | Api |
| `Microsoft.OpenApi` | 2.12.0 | (transitivo, fijado centralmente) |
| `xunit` | 2.9.3 | pruebas |
| `xunit.runner.visualstudio` | 3.1.4 | pruebas |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | pruebas |
| `coverlet.collector` | 6.0.4 | pruebas |

`Microsoft.EntityFrameworkCore.Design` lleva `PrivateAssets=all`: solo se necesita para **crear** migraciones, no para ejecutar la aplicación. Así no se propaga a los proyectos que referencian Infraestructura ni se incluye al publicar el contenedor.

---

## 6. Problemas encontrados y cómo se resolvieron

Bitácora real, por si vuelven a aparecer.

### 6.1 `dotnet new sln` genera `.slnx`, no `.sln`

El SDK de .NET 10 usa por defecto el **formato nuevo de solución**: `Maquinaria.slnx`, XML limpio en lugar del formato propietario del `.sln` clásico (que llevaba GUIDs duplicados por proyecto y producía conflictos de merge horribles).

Cualquier comando que diga `Maquinaria.sln` falla con `MSBUILD : error MSB1009`. Visual Studio 2026 abre `.slnx` nativamente.

### 6.2 `Get-ChildItem` pasado a un ejecutable externo pierde la ruta

```powershell
# FALLA: "No se encuentra el proyecto o directorio Maquinaria.Api.csproj"
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj)

# CORRECTO
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj | Select-Object -ExpandProperty FullName)
```

`Get-ChildItem` devuelve objetos `FileInfo`. Al pasarlos a un programa externo, PowerShell los convierte a texto con `.ToString()`, que para un `FileInfo` obtenido con `-Filter` da **solo el nombre del archivo**, sin ruta.

### 6.3 cmd no entiende cmdlets de PowerShell

`Get-ChildItem`, `Select-Object` y compañía existen solo en PowerShell. En cmd producen *"no se reconoce como un comando interno o externo"*.

Un script `.ps1` **sí** corre desde cmd, porque `powershell -File` lanza PowerShell como subproceso:

```
powershell -ExecutionPolicy Bypass -File .\scaffold.ps1
```

El `-ExecutionPolicy Bypass` es necesario porque Windows bloquea scripts `.ps1` sin firmar; así se evita cambiar la política del sistema.

**Terminal de trabajo del proyecto: PowerShell.**

### 6.4 NU1903 — vulnerabilidad en `Microsoft.OpenApi 2.0.0`

```
warning NU1903: El paquete "Microsoft.OpenApi" 2.0.0 tiene una vulnerabilidad
de gravedad alta conocida  (GHSA-v5pm-xwqc-g5wc)
```

La plantilla `webapi` referenciaba `Microsoft.AspNetCore.OpenApi 10.0.10`, que arrastraba **transitivamente** la versión comprometida. Al no estar en ningún `.csproj`, no se podía cambiar directamente.

Resuelto actualizando a `Microsoft.AspNetCore.OpenApi 10.0.11` (que ya trae `Microsoft.OpenApi 2.7.5`) y declarando `Microsoft.OpenApi 2.12.0` en `Directory.Packages.props` con *transitive pinning* activo.

### 6.5 `dotnet` no busca la solución hacia arriba

`dotnet sln add` busca el archivo de solución en el directorio **actual** únicamente. Si el prompt no termina en `maquinaria_back`, falla con *"no hay ningún archivo de solución en el directorio"*.

---

## 7. Estado y pendientes

**Hecho:**

- [x] Neon: proyecto, rama `dev`, extensiones verificadas
- [x] Neon permite `CREATE DATABASE` — **bloqueante del modelo multi-database**, verificado
- [x] Repo `maquinaria_back` con los 6 proyectos y sus referencias
- [x] Central Package Management + paquetes de EF Core
- [x] Repo `maquinaria_front` con Angular 22 (zoneless, `AGENTS.md`, MCP de Angular)
- [x] `dotnet build` en verde, sin vulnerabilidades
- [x] `dotnet user-secrets init`

**Pendiente:**

- [ ] Confirmar que la región de Neon coincide con la que se usará en Railway
- [ ] Cargar las dos cadenas de conexión de `dev` en user secrets
- [ ] Remotos de GitHub para los dos repos
- [ ] `ContextoCentral` + sus 5 entidades + primera migración (`05-esquema-fase0.md` §3)
- [ ] `ContextoEmpresa` + sus 10 entidades + su migración (§4)
- [ ] Servicio de aprovisionamiento y comando `migrar-empresas` (§5)
- [ ] Convenciones de trabajo en equipo: ramas, commits, revisión, acceso a Neon

---

## 8. Reconstruir desde cero

1. Instalar SDK de .NET 10, Node 24+, Git y Visual Studio 2026.
2. `npm install -g @angular/cli` y `dotnet tool install --global dotnet-ef`.
3. Clonar los dos repositorios en una carpeta contenedora común.
4. Crear el proyecto y la rama de Neon (§2), o pedir acceso al existente.
5. Configurar los user secrets (§2.5).
6. `dotnet build Maquinaria.slnx` y `dotnet ef database update`.

No hay que volver a correr el andamiaje: eso ya vive en el repositorio.
