# Estado y pendientes

Última verificación: 2026-08-20.

## Estado actual

**La base central existe.** Las 5 tablas de plataforma están creadas y migradas en `maquinaria_central` (Neon, rama `dev`), con sus `CHECK`, sus índices y el constraint `EXCLUDE` de no-traslape verificados contra la base real. `dotnet build --no-incremental` en verde con 0 advertencias y sin paquetes vulnerables. Todavía no hay un solo endpoint de negocio: `/openapi/v1.json` sigue con `"paths": { }`.

### Hecho

- [x] Neon: proyecto `maquinaria`, rama `dev`, región N. Virginia confirmada, extensiones verificadas
- [x] Verificado que Neon permite `CREATE DATABASE` — bloqueante del modelo multi-database
- [x] Solución `Maquinaria.slnx` con los 6 proyectos y sus referencias
- [x] Central Package Management con transitive pinning
- [x] Paquetes de EF Core, Npgsql y OpenAPI
- [x] `dotnet user-secrets init` en `Maquinaria.Api`
- [x] Bases `maquinaria_central` y `maquinaria_plantilla` creadas en la rama `dev`
- [x] Secretos `ConnectionStrings:Central` (pooled) y `ConnectionStrings:Migraciones` (directa), **ambas verificadas conectando** contra Neon
- [x] Las cuatro decisiones previas a la primera migración, cerradas (ver abajo)
- [x] `.editorconfig` del backend, y `dotnet-ef` 10.0.11 como herramienta local (`dotnet-tools.json` en la **raíz**, no en `.config/`: el `.gitignore` de .NET ignora esa carpeta y el manifiesto quedaría sin rastrear)
- [x] `ContextoCentral`, sus 5 entidades y sus 5 configuraciones
- [x] Migración `CentralInicial` **aplicada** a `maquinaria_central`. `EXCLUDE`, `CHECK` de formato de `nombre_bd` y `slug`, rango de enums, `UNIQUE` y `numeric(18,4)`: los once probados contra la base real
- [x] Migración `CentralSemillaPlanBase` aplicada: un plan `base` **provisional** (precio 0, límites en `-1`) para desbloquear el aprovisionamiento. El catálogo comercial sigue abierto en [`04-pendientes.md`](../04-pendientes.md)
- [x] `Microsoft.EntityFrameworkCore.Design` retirado de `Infraestructura`: lo exige el proyecto de arranque, no el que contiene el `DbContext`

### Pendiente de Fase 0

- [ ] `Dockerfile` para el despliegue en Railway
- [ ] `ContextoEmpresa` + sus 10 entidades + su migración
- [ ] Servicio de aprovisionamiento y comando `migrar-empresas`
- [ ] Resolución de conexión por empresa + interceptor de auditoría
- [ ] Auth completo: login por empresa/correo/contraseña, JWT, refresh rotativo, invitaciones
- [ ] Manejo global de errores, logging estructurado, health checks
- [ ] Abstracción de almacenamiento de archivos con implementación en disco
- [ ] Convenciones de equipo: ramas, commits, revisión, acceso a Neon (los remotos de GitHub ya están: `xYairrx/maquinaria-backend` y `xYairrx/maquinaria-frontend`, rama `develop`)

### Criterio de salida de Fase 0

Un superadministrador da de alta una empresa desde el panel, el sistema le crea y migra su base automáticamente, se envía la invitación al primer administrador, esa persona define su contraseña e inicia sesión con `empresa / correo / contraseña`. Y el comando `migrar-empresas` aplica una migración nueva a todas las bases existentes reportando el resultado por empresa.

### Orden de trabajo

El paso **7** está cerrado. El siguiente es el **8**: `ContextoEmpresa` con sus 10 entidades y su migración, más su fábrica de tiempo de diseño apuntando a `maquinaria_plantilla`. El DDL de referencia está en [`05-esquema-fase0.md`](../05-esquema-fase0.md) §4.

El método es por **rebanadas verticales**: `Entidad → Migración → Caso de uso → Endpoint → Pruebas → Pantalla Angular → Funciona`. No "todo el backend y luego todo el frontend".

---

## Decisiones cerradas antes de la primera migración

Cuatro huecos que los documentos de diseño no cerraban. Se resolvieron el **2026-08-20**, antes de la primera migración, porque la regla *append-only* los vuelve irreversibles.

### 1. Carpeta y nomenclatura de las migraciones

Con dos `DbContext` en el mismo assembly hay que separarlas físicamente:

```
src/Maquinaria.Infraestructura/Migraciones/Central/
src/Maquinaria.Infraestructura/Migraciones/Empresa/
```

Se logra con `--output-dir`; EF Core deriva el *namespace* de la carpeta. Ejemplo:

```bash
dotnet ef migrations add CentralInicial --context ContextoCentral --output-dir Migraciones/Central --project src/Maquinaria.Infraestructura --startup-project src/Maquinaria.Api
```

**Los nombres llevan prefijo del contexto** — `CentralInicial`, `EmpresaInicial`. No porque la carpeta no desambigüe, sino porque `dotnet ef migrations list` y los logs de despliegue muestran **solo el nombre**: con dos migraciones llamadas `Inicial`, el reporte de `migrar-empresas` sería ambiguo. Y renombrarlas después viola *append-only*.

No hay riesgo de colisión entre los dos juegos: cada base tiene su propia `__EFMigrationsHistory`, y la central y las de empresa son bases distintas.

### 2. Mapeo PascalCase → snake_case

**`EFCore.NamingConventions`** (versión 10.0.1 — el paquete sigue el versionado de EF Core, major 10 = EF Core 10), con `UseSnakeCaseNamingConvention()` en cada contexto:

```csharp
options.UseNpgsql(cadena).UseSnakeCaseNamingConvention();
```

Traduce tablas, columnas, índices, constraints y llaves. Las alternativas se descartaron por costo: `HasColumnName` por propiedad son ~500 líneas de mapeo con 75 entidades, y una convención propia son ~40 líneas que hay que mantener y probar.

**Es decisión de Fase 0 y no de después:** el paquete cambia el esquema generado, así que agregarlo tras la primera migración obliga a una migración de renombre masivo.

Ojo al escribir las entidades: la convención traduce el nombre del `DbSet`, así que `DbSet<Usuario> Usuarios` produciría la tabla `usuarios`. El DDL de diseño usa **singular** (`usuario`), así que el nombre se fija explícitamente con `ToTable()`.

### 3. `IDesignTimeDbContextFactory` para `ContextoEmpresa`

Los dos contextos **no** tienen el mismo problema:

- **`ContextoCentral` la necesita por la cadena de conexión.** Sin fábrica, `dotnet ef` construye el host y toma el contexto del contenedor de DI — con la cadena `Central`, que es la **pooled**, y por ahí no se puede correr DDL. La fábrica lo fuerza a `Migraciones`, la directa. (Corregido el 2026-08-20: la primera versión de esta decisión decía que no hacía falta.)
- **`ContextoEmpresa` sí.** No tiene cadena fija —se resuelve por petición— así que EF no tiene de dónde sacarla.

**Va en `src/Maquinaria.Api/TiempoDiseno/`**, no en `Infraestructura`. EF Core busca la fábrica en el assembly de las migraciones *o* en el proyecto de arranque. Ponerla en `Infraestructura` obligaría a agregarle tres paquetes de configuración (`Configuration.UserSecrets`, `.Json`, `.EnvironmentVariables`) más el GUID de secretos duplicado, porque `Infraestructura` no puede referenciar `Api` sin ciclo. En `Api` lee la configuración igual que la aplicación real y **no agrega ni un paquete**.

Es un compromiso consciente: código de tiempo de diseño en el proyecto de API es raro, pero la alternativa cuesta cuatro dependencias y un secreto duplicado para lo mismo.

**La de `ContextoEmpresa` no lleva cadena propia.** Toma `ConnectionStrings:Migraciones` y le sustituye el `Database=` por `maquinaria_plantilla` con `NpgsqlConnectionStringBuilder`. Así no hay un tercer secreto que se desincronice, y el camino de código es el mismo que usa el runtime con cada empresa.

### 4. Nombres de las bases de datos

Dos bases, no una:

| Base | Para qué |
|---|---|
| `maquinaria_central` | `ContextoCentral`. Sustituye al `neondb` por defecto de Neon |
| `maquinaria_plantilla` | `ContextoEmpresa` **solo en tiempo de diseño** |

La plantilla existe por un peligro que se deriva de la decisión 3: en cuanto haya fábrica para `ContextoEmpresa`, un `dotnet ef database update --context ContextoEmpresa` distraído aplicaría migraciones a la base a la que apunte esa cadena — la central, o peor, la de un cliente fuera del proceso controlado de `migrar-empresas`. Apuntando a una base vacía, ese comando no puede hacer daño, y de paso da dónde inspeccionar el esquema de empresa generado contra §4 de [`05-esquema-fase0.md`](../05-esquema-fase0.md).

En Neon no cuesta nada: mismo proyecto, cobro por almacenamiento.

**Consecuencia: hace falta una lista de slugs reservados.** Un tenant con slug `plantilla` generaría `nombre_bd = maquinaria_plantilla` y chocaría; igual `central`. La validación del alta debe rechazar al menos `central`, `plantilla`, `admin`, `api`, `www`, `app`. Esto no está en ningún documento de diseño — se detectó al cerrar esta decisión.

---

## Restricciones del aprovisionamiento

Cuando se implemente el alta de empresas, la secuencia es:

```
1. INSERT en tenant                    → estado_aprovisionamiento = Pendiente
2. CREATE DATABASE maquinaria_<slug>   → Creando
3. Migraciones de ContextoEmpresa en esa base
4. Semillas: permisos, los 9 roles, parametros por defecto
5. Crear el primer usuario administrador (sin contrasena)
6. Emitir su token de invitacion y enviarlo
7. estado_aprovisionamiento = Lista, version_esquema = <ultima migracion>
```

Cuatro restricciones técnicas que el código debe respetar desde el día uno:

1. **`CREATE DATABASE` no corre dentro de una transacción** y EF Core envuelve en transacción por defecto. Hay que abrir una `NpgsqlConnection` directa contra la central y ejecutar el comando fuera de transacción.
2. **El nombre de la base no se puede parametrizar** en SQL, así que se concatena. Revalidar el formato con regex en C# antes de concatenar es control de seguridad, no cosmética.
3. **Los pasos 1 y 2 no son atómicos.** `estado_aprovisionamiento = Fallida` deja el registro reintentable en lugar de un huérfano.
4. **Es lento.** Al inicio va en línea; después conviene moverlo a un `BackgroundService` y que la UI consulte `estado_aprovisionamiento`.

---

## Divergencias con los documentos de diseño

Los documentos de [`docs/`](../) son especificación, no inventario. Diferencias detectadas al 2026-08-20:

| Documento dice | Realidad |
|---|---|
| Repos `maquinaria_back` y `maquinaria_front` | `maquinaria-backend` y `maquinaria-frontend` |
| Contenedor en `Documents/Maquinaria/` | `OneDrive/Desktop/maquinaria/` |
| Frontend en Angular 22 / CLI 22.1.4 | Angular 21.2.21 |
| Checklist marca el andamiaje del backend como hecho | Se creó el 2026-08-20 |

Verifica siempre contra el repo antes de asumir que algo está hecho.
