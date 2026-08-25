# Estado y pendientes

Última verificación: 2026-08-25.

## Estado actual

**El esquema de la Fase 0 está completo y el primer login funciona.** Las 9 tablas de plataforma en `maquinaria_central` y las 10 de empresa en `maquinaria_plantilla` (Neon, rama `dev`), con sus `CHECK`, sus índices y el constraint `EXCLUDE` de no-traslape verificados contra la base real.

**`/openapi/v1.json` ya no está vacío:** expone `POST /api/plataforma/sesion` y `GET /api/plataforma/sesion/actual`. Un superadministrador inicia sesión, recibe un JWT y accede a un endpoint protegido, comprobado de punta a punta contra Neon.

**El ciclo completo de credenciales de un usuario de empresa está cerrado** (2026-08-24): invitación, definición de contraseña, login con slug y restablecimiento. Y **el navegador ya puede hablar con la API**: el CORS pasó de lista fija a predicado sobre un dominio base, porque cada empresa vive en su propio subdominio.

**Al 2026-08-25 se cerraron las tres piezas que faltaban del ciclo de vida de una empresa**:
el comando `migrar-empresas` con su endpoint de salud de esquemas, el refresco rotativo de la
sesión de empresa y el reintento de un alta en `Fallida`. Las tres eran mecánica pequeña sobre
código que ya existía, y la última destapó **un agujero de seguridad en el sembrador del
administrador**, que se cerró el mismo día y tiene
[sección propia](#el-agujero-del-sembrador-de-administradores--2026-08-25) porque es lo más
importante de la jornada. Lo que sigue abierto de la Fase 0 es todo transversal: el
interceptor de auditoría —el único de la secuencia de arranque que falta—, el logging con
`correlacion_id`, el almacenamiento de archivos, el `Dockerfile` y las convenciones de equipo.

**Medido corriendo las herramientas el 2026-08-25:**
`dotnet build Maquinaria.slnx --no-incremental` da **compilación correcta, 0 errores**; la
única advertencia es el `MSB3061` de la DLL que un proceso vivo de `Maquinaria.Api` tiene
tomada, que no es código sino la [trampa de operación](#trampa-de-operación-dos-instancias-de-la-api-a-la-vez)
ya conocida. `dotnet test` deja `Maquinaria.Api.Tests` en **205 pruebas, 0 fallos**, y
`Maquinaria.Dominio.Tests` en 1. **La cifra de 116 que este documento traía era del
2026-08-24** y estaba vieja: ver [las cifras corregidas](#cifras-que-la-propia-bitácora-tenía-mal).

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
- [x] `ContextoCentral`, sus 8 entidades y sus 8 configuraciones
- [x] Migración `CentralInicial` **aplicada** a `maquinaria_central`. `EXCLUDE`, `CHECK` de formato de `nombre_bd` y `slug`, rango de enums, `UNIQUE` y `numeric(18,4)`: probados contra la base real
- [x] Migración `CentralSemillaCatalogos` aplicada: los 18 módulos conocidos, los 4 tipos de límite y un plan `base` **provisional** (precio 0, todos los módulos, sin límites). El catálogo comercial sigue abierto en [`04-pendientes.md`](../04-pendientes.md)
- [x] Los 14 constraints nuevos y modificados verificados contra la base real, caso por caso: los 14 rechazos esperados los emite el motor (`23514` CHECK, `23505` UNIQUE, `23503` FK, `23001` RESTRICT, `23P01` EXCLUDE), y los positivos —cupo `0` y cupo `-1`— se aceptan
- [x] `Microsoft.EntityFrameworkCore.Design` retirado de `Infraestructura`: lo exige el proyecto de arranque, no el que contiene el `DbContext`

### Pendiente de Fase 0

- [ ] `Dockerfile` para el despliegue en Railway
- [x] `ContextoEmpresa`, su fábrica de tiempo de diseño apuntando a `maquinaria_plantilla`, y las **7 entidades de autenticación y permisos** con sus 7 configuraciones
- [x] Migraciones `EmpresaInicial` y `EmpresaSemillaSeguridad` **aplicadas** a `maquinaria_plantilla`: extensiones `btree_gist` y `pg_trgm`, el trigger `rol_sistema_inmutable`, 108 permisos y los 9 roles
- [x] Los 18 constraints y el trigger de la base de empresa verificados contra la base real, caso por caso, más 3 casos positivos
- [x] Las 3 tablas restantes de `ContextoEmpresa` —`parametro`, `archivo`, `auditoria`— con el trigger `auditoria_inmutable`, en `EmpresaAuditoriaYConfiguracion`
- [x] `auditoria` **también en la base central** (`CentralAuditoria`), con su trigger. La misma entidad en los dos contextos
- [x] Los 12 constraints de `auditoria` verificados contra **las dos** bases reales, y las 6 preguntas que la bitácora debe responder, comprobadas
- [ ] El interceptor de auditoría: **ya no está bloqueado** (2026-08-24). Necesitaba `usuario_id`, `roles`, `ip` y `origen` del contexto de la petición autenticada, y con la auth de empresa cerrada esos cuatro existen. Queda por escribir
- [x] **Servicio de aprovisionamiento**, con su endpoint `POST /api/plataforma/empresas`. Probado creando una empresa real de punta a punta
- [x] Abstracción de correo `IEnviadorCorreo`, con `CorreoEnLog` para desarrollo y `CorreoResend` para la nube
- [x] `GET /api/plataforma/empresas`: listado con estado de aprovisionamiento, plan y módulos. Usa subconsultas y no joins, para que un tenant **sin** suscripción aparezca con plan nulo en lugar de desaparecer — que son justo los que hay que ver
- [x] **Catálogo comercial**: `GET /api/plataforma/planes`, `POST /api/plataforma/planes`,
  `PATCH /api/plataforma/planes/{codigo}/activo` y `GET /api/plataforma/modulos` (2026-08-25).
  Es la salida del pendiente de planes de [`04-pendientes.md`](../04-pendientes.md) §3, que
  pedía expresamente que los precios se carguen desde el panel y no por migración
  —serían *append-only* y cambiar un precio exigiría un despliegue—.

  **El plan es su conjunto de módulos**, no un paquete de cupos: los cupos siguen colgando
  del tenant en `tenant_limite`. Un plan sin módulos se rechaza, porque la empresa que lo
  contratara entraría y no vería ni una pantalla, sin ningún error de por medio.

  **No hay PUT ni PATCH del plan completo, y es una decisión**: ver los dos huecos que la
  cierran en la sección de abajo. Lo que sí se puede es retirar un plan y crear su sucesor.
- [x] **Comando `migrar-empresas` + endpoint de salud** que reporta quién quedó atrasado
  (2026-08-25). Es un argumento de `Maquinaria.Api`, no un proyecto de consola aparte, y
  `GET /api/plataforma/salud/esquemas` es lo que vuelve visible el desfase. Ver la sección
  de abajo
- [x] Endpoint para **reintentar** un alta en `Fallida` (2026-08-25):
  `POST /api/plataforma/empresas/{slug}/reintento`. Corre el **mismo** código que el alta
  —los pasos 2 a 6 extraídos a `EjecutarSecuenciaAsync`—, no una copia
- [ ] Comando `migrar-empresas` + endpoint de salud que reporte quién quedó atrasado
- [x] Comando **`migrar-empresas`** con reporte por empresa, y **`GET /api/plataforma/esquema`** que dice quién quedó atrasado. Ver [`06-alcance-fase1.md`](../06-alcance-fase1.md) §8
- [x] Diagnosticado el estado real de las cuatro bases (ver la nota de abajo)
- [x] **Las 28 tablas de Fase 1 estan en las tres bases** (38 con las 10 de Fase 0), 7 migraciones, huella comun `b4f101c3…`, y **30 de 30 pruebas de garantias en verde** contra la base real
- [ ] Endpoint para **reintentar** un alta en `Fallida`. La secuencia ya es idempotente; falta el disparador
- [x] **Resolución de conexión por empresa**: `IDirectorioTenants` con caché, `IContextoTenant` de ámbito de petición, `MiddlewareTenant`, `FabricaConexionesEmpresa` y `ProveedorContextoEmpresa`. Ver [`01-arquitectura.md`](../01-arquitectura.md) §2.0
- [x] Auth de **empresa**, completa (2026-08-25): invitaciones, login por `empresa / correo / contraseña`, **restablecimiento de contraseña** (2026-08-24) y **refresco rotativo** de la `sesion_refresh`
- [x] **CORS por subdominio** y acceso desde el navegador (2026-08-24). Ver la sección de abajo
- [x] Manejo global de errores (`IExceptionHandler` → ProblemDetails, sin filtrar mensajes de excepción al cliente) y health check `/salud` de la base central
- [x] Auth de **plataforma**: PBKDF2, JWT con audiencia propia, policy de ámbito, limitador de intentos por IP, y siembra del primer superadministrador desde secretos
- [x] **Endpoint de refresco rotativo** de la sesión de empresa (2026-08-25):
  `POST /api/empresas/{slug}/sesion/refresco`, anónimo, con detección de reuso que revoca
  toda la cadena del usuario. Ver la sección de abajo, y en particular la trampa que
  hereda el cliente: **la rotación no tiene ventana de gracia**
- [ ] Logging estructurado con enriquecimiento por petición (falta el `correlacion_id` que compartirá con la auditoría)
- [ ] Abstracción de almacenamiento de archivos con implementación en disco
- [ ] Convenciones de equipo: ramas, commits, revisión, acceso a Neon (los remotos de GitHub ya están: `xYairrx/maquinaria-backend` y `xYairrx/maquinaria-frontend`, rama `develop`)

### Criterio de salida de Fase 0

Un superadministrador da de alta una empresa desde el panel, el sistema le crea y migra su base automáticamente, se envía la invitación al primer administrador, esa persona define su contraseña e inicia sesión con `empresa / correo / contraseña`. Y el comando `migrar-empresas` aplica una migración nueva a todas las bases existentes reportando el resultado por empresa.

**Todas las piezas de ese enunciado existen en disco al 2026-08-25**, incluida la última que
faltaba, `migrar-empresas`. Con dos salvedades que hay que decir en voz alta, porque «existe»
y «demostrado» no son lo mismo:

- **`migrar-empresas` no se ha corrido contra Neon.** La prueba de humo fue con las cadenas
  apuntando a `127.0.0.1:1`, que verifica el cableado y el código de salida `2` y **no aplica
  ninguna migración**. Hasta que el operador lo corra, `demo` y `bajio` siguen atrás.
- **El envío de correo real no se ha ejercitado.** `CorreoResend` es el camino activo, pero
  falta `Resend:Llave` y un dominio verificado, así que la invitación del criterio de salida
  se ha probado con el proveedor de log y con la liga devuelta en la respuesta, que solo
  funciona en Development.

Y lo que queda abierto de la Fase 0 no está en el criterio de salida: es lo transversal
—interceptor de auditoría, logging con `correlacion_id`, almacenamiento de archivos,
`Dockerfile`, convenciones de equipo—. Se dice explícitamente para que nadie lea la lista de
arriba y concluya que la fase está cerrada: **el criterio se cumple en código, la fase no**.

### Orden de trabajo

Cerrados los pasos **7**, **8**, **9**, **10** y el **12** completo. Del **11** está hecha la
resolución de conexión por empresa y falta el interceptor de auditoría, que es la otra mitad
de ese paso. El orden que sigue, revisado el 2026-08-25:

1. **Interceptor de auditoría** (parte del paso 11). **Sube a primer lugar y es el único de
   la secuencia de arranque que falta.** Está desbloqueado desde el 2026-08-24: hay contexto
   de petición autenticada de los dos lados —plataforma y empresa—, así que `usuario_id`,
   `roles`, `ip` y `origen` existen. Las dos tablas `auditoria` están construidas y vacías, y
   siguen así mientras no exista el interceptor; lo que se audita hoy no se audita en ninguna
   parte. Ojo con dos cosas ya escritas: la lista de propiedades excluidas —`hash_contrasena`
   y los `hash_token` **nunca** entran al `jsonb`— y que los contextos que construye
   `ProveedorContextoEmpresa` **no llevan interceptores** a propósito, así que el
   aprovisionamiento y `migrar-empresas` seguirán sin auditar fila por fila.
2. Lo transversal que queda de la fase, en el orden en que estorbe: logging estructurado con
   `correlacion_id` —que es el puente con la auditoría, así que conviene justo después del
   punto 1—, `IAlmacenamientoArchivos` con implementación en disco, el `Dockerfile` de
   Railway y las convenciones de equipo.

Lo que salió de esta lista el 2026-08-25:

- ~~Resolución de conexión por empresa.~~ **Hecha.**
- ~~Aprovisionamiento.~~ **Hecho**, y ahora también su reintento.
- ~~Auth de empresa (el 12).~~ **Completa**: invitación, definición de contraseña, login con
  slug, restablecimiento y refresco rotativo.
- ~~Comando `migrar-empresas` (el 10) y el endpoint de salud.~~ **Hechos.** Era el punto de
  mayor prioridad porque el desfase ya había dejado de ser hipotético.
- ~~Endpoint de refresco rotativo y endpoint de reintento.~~ **Hechos.**

El DDL de referencia está en [`05-esquema-fase0.md`](../05-esquema-fase0.md).

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

## Rediseño del esquema central — 2026-08-21

Decisión de producto que cambió la forma de la base central de 5 tablas a 8, tomada **antes** de que exista el primer tenant.

### Qué cambió

| Antes | Ahora |
|---|---|
| El plan llevaba cupos (`plan_limite`) | El plan lleva **módulos** (`modulo`, `plan_modulo`) |
| Los cupos colgaban del plan | Los cupos cuelgan del **tenant** (`tipo_limite`, `tenant_limite`) |
| `plan_limite.clave` era texto libre | `tenant_limite.tipo_limite_id` es FK a un catálogo |
| `usuario_plataforma` | `usuario` |

**`plan_limite` no se renombró: desapareció.** El diseño anterior obligaba a inventar un plan a medida para el cliente que negociaba un cupo distinto, y eso ensucia el catálogo comercial. Separando *qué módulos* de *cuánto*, cada cosa cambia sin arrastrar a la otra.

El detalle de DDL y las consecuencias de autorización están en [`05-esquema-fase0.md`](../05-esquema-fase0.md) §3.

### Por qué el rename fue a `usuario` y no a `usuarios`

La decisión 2 de este documento fija tablas en **singular** con `ToTable()` explícito, porque `EFCore.NamingConventions` traduce el nombre del `DbSet`. Las otras ~74 tablas del sistema van en singular, y `plan_modulo`, `rol_permiso` y `usuario_rol` fijan el patrón padre-hijo también en singular.

Queda homónima de la `usuario` de la base de empresa, y eso es deliberado: son bases distintas, así que en SQL no hay colisión, y en C# el error no compila porque cada entidad existe solo en su propio `DbContext`.

### Se rompió el append-only de las migraciones centrales, a propósito

Las migraciones `CentralInicial` y `CentralSemillaPlanBase` del 2026-08-20 **se borraron y se regeneraron**, y `maquinaria_central` se recreó desde cero.

Eso viola la regla de que el historial es *append-only*. Se hizo con conocimiento de causa porque las dos condiciones que justifican la regla no se cumplían todavía:

1. **La central es una sola base, nuestra.** El *append-only* existe porque una base de empresa puede estar dos versiones atrás y tiene que poder alcanzar. Con una sola base no hay desfase posible.
2. **No había dato que perder.** Verificado antes de tocar nada: 1 plan y 4 límites de la semilla provisional, **0 tenants, 0 suscripciones, 0 superadmins**.

**Esa puerta ya está cerrada.** En cuanto exista el primer tenant real, o en cuanto `ContextoEmpresa` se aplique a una base de cliente, un cambio de esquema es una migración nueva y nunca una regeneración.

### Lo que quedó abierto

- ~~Faltan 12 de los 30 módulos.~~ **Resuelto el 2026-08-21**: el `.docx` entró al repositorio y el catálogo quedó completo con los **26** módulos reales. Ver abajo.
- **Nada aplica los límites todavía.** `tenant_limite` y `tipo_limite` existen, pero no hay un caso de uso que los lea. Cuando se escriba: el límite está en la central y el consumo en la base de la empresa, sin transacción que abarque las dos.
- **La segunda compuerta de autorización no existe.** Con los módulos definiendo el plan, el permiso efectivo es `permisos del rol ∩ módulos del plan`, y `permiso.modulo` (base de empresa) referencia `modulo.clave` (central) **sin FK posible**. Hace falta la prueba en CI que verifique esa correspondencia.
- **¿`tenant_modulo`?** Como el plan *es* su conjunto de módulos, un cliente que quiera un módulo extra necesita otro plan. Si el caso aparece, hará falta una tabla de excepción espejo de `tenant_limite`. Fuera de alcance hasta entonces.

---

## Control de usuarios y permisos — 2026-08-21

Paso 8, primera rebanada: las 7 tablas de autenticación y permisos, aplicadas a `maquinaria_plantilla`. El DDL de referencia y el razonamiento completo están en [`05-esquema-fase0.md`](../05-esquema-fase0.md) §4.

### Decisiones de producto que se cerraron

**1. Los usuarios no se borran: viven en un estado.** `usuario.activo` + `usuario.eliminado_en` se sustituyeron por un solo `estado` — `Invitado`, `Activo`, `Suspendido`, `Baja`. El par anterior permitía cuatro combinaciones de las que dos eran basura, y "sin contraseña" era una inferencia sobre `hash_contrasena IS NULL` en lugar de un estado explícito.

**Consecuencia aceptada:** `UNIQUE (correo)` es global, así que **un correo nunca se libera**. La alternativa —único solo entre los que no están de baja— volvería ambiguo el login, que tendría que filtrar por estado antes de validar. Es regla escrita que los correos no se reciclan.

**2. `eliminado_en` es baja lógica y nunca física.** Donde sobrevive —`tenant` y `archivo`— no hay `DELETE`. En `archivo` además marca cuándo dejó de existir el binario en R2, que es información que un estado no daría.

**3. El rol `administrador` salta la verificación de permisos, y `rol_permiso` se siembra vacía.** El reparto lo define el administrador de cada empresa, porque en una empresa ventas autoriza y en otra solo cotiza.

Eso obligó a **separar `es_sistema` de `acceso_total`**. `es_sistema` marca los nueve roles semilla y solo impide borrarlos; si además significara "salta la verificación", los nueve la saltarían y la empresa quedaría abierta. `acceso_total` va solo en `administrador`, y es una columna y no una comparación contra `codigo = 'administrador'` porque las empresas renombran los roles.

Tres garantías, todas en el motor:

| garantía | cómo |
|---|---|
| como máximo un rol con acceso total | `UNIQUE INDEX rol_acceso_total_unico ON rol (acceso_total) WHERE acceso_total` |
| ese rol no se edita ni se borra ni se le apaga el acceso | trigger `rol_sistema_inmutable`, con `WHEN (OLD.es_sistema AND OLD.acceso_total)` |
| los otros 8 siguen siendo renombrables | el `WHEN` apunta a las **dos** banderas, no a `es_sistema` solo |

Y como `acceso_total` no se puede apagar, la regla *"debe quedar al menos un rol con acceso total"* se cumple sola: no hace falta el constraint diferido que se había considerado.

**4. El rol `administrador` no aparece en la interfaz de asignaciones.** Se otorga solo al aprovisionar. **La empresa tiene entonces exactamente una persona con acceso total**, y si esa persona se va, solo la plataforma puede nombrar otra. Esa operación de recuperación **hace falta implementarla** y se audita con `origen = 'plataforma'`.

### La segunda compuerta de autorización

Con los módulos definiendo el plan, el permiso efectivo es una **intersección**, no una lectura:

```
permisos del rol  ∩  módulos del plan del tenant
```

Un usuario con `logistica.crear` en una empresa cuyo plan no incluye logística **no** puede crear un flete. Hay que implementarlo en la resolución de permisos, no dejarlo para después, o el permiso concedido gana.

`permiso.modulo` (base de empresa) referencia `modulo.clave` (central) **sin FK posible**: son bases distintas. Sigue pendiente la prueba en CI que verifique la correspondencia.

### La regla de las semillas que hay que respetar

`EmpresaSemillaSeguridad` **congela los 18 módulos y las 6 acciones en su propio texto**; no lee `ClavesModulo` ni `AccionesPermiso`.

Es deliberado y es la regla más importante de una semilla: una migración tiene que producir el mismo resultado en toda base donde se aplique, hoy y en dos años. Si leyera las constantes de C#, agregar un módulo cambiaría el SQL de una migración **ya aplicada**, y una base nueva recibiría permisos que las viejas no tienen. Los 12 módulos que faltan reciben sus permisos en una migración **nueva**.

### `auditoria`: diseñada, no construida

Queda especificada por completo en [`05-esquema-fase0.md`](../05-esquema-fase0.md) §4 y va en la **segunda** migración, por una dependencia de orden: el interceptor necesita `usuario_id`, `roles`, `ip` y `origen`, que salen del contexto de la petición autenticada, así que no se puede escribir antes que la auth. Y la tabla sin interceptor no sirve de nada.

Lo que cambió respecto al diseño original:

- **Tres campos nuevos.** `correlacion_id` (¿qué se hizo en una sola acción?), `usuario_correo` congelado (¿quién fue?), y `roles` (¿por qué se le permitió? — obligatorio ahora que el administrador salta la verificación).
- **`accion` pasa de 3 a 8 valores.** Un interceptor de `SaveChanges` solo ve escrituras, y las acciones que más importan auditar de alguien con acceso total —`Acceso`, `Exportacion`— no modifican ni una fila.
- **`accion = 3` se llama `Borrado`, no `Baja`.** Significa "la fila desapareció"; la baja de un usuario es `2 Cambio`.
- **Append-only garantizado por un trigger.** Un registro que el propio auditado puede borrar no es un registro.
- **Sin columna `nivel`.** La auditoría y el log técnico son dos sistemas; el puente entre ellos es `correlacion_id`, que va en ambos.
- **La lista de propiedades excluidas no es opcional:** `hash_contrasena` y los `hash_token` nunca entran a `jsonb`. Es lo único de la auditoría que, mal hecho, es peor que no tenerla.

**Y hace falta también en la base central**, que hoy no tiene. Dar de alta un tenant, suspenderlo, cambiarle el plan o moverle un `tenant_limite` ocurre solo allí y no queda registrado en ninguna parte — y son las decisiones más privilegiadas del sistema.

### Detalle técnico que costó un intento

`SesionRefresh.Ip` se declaró primero como `string` con `HasColumnType("inet")`, y Npgsql lo rechaza: no mapea `string` a `inet`. El tipo correcto es `System.Net.IPAddress`, que además es del BCL, así que `Maquinaria.Dominio` no gana ninguna dependencia de infraestructura — el mismo criterio que descartó `NpgsqlRange<T>`. Aplica igual a `auditoria.ip`.

### Pendiente de decidir

- **La política de retención de la auditoría.** Cuándo se particiona por `fecha_utc` es técnico y puede esperar; *cuánto tiempo se conserva* no lo es, y con `ip` de por medio hay implicaciones de datos personales. Conviene responderlo antes de vender el primer contrato.
- ~~Los 12 módulos faltantes.~~ **Resuelto.**

### La bitácora quedó construida, el interceptor no

`parametro`, `archivo` y `auditoria` se aplicaron en `EmpresaAuditoriaYConfiguracion`, y `auditoria` también en la central con `CentralAuditoria`. **La misma entidad `Maquinaria.Dominio.Trazabilidad.Auditoria` se configura en los dos contextos**: la tabla no tiene ni una relación, así que duplicar la clase no compraría nada. Las dos configuraciones se generaron desde una sola plantilla para que no puedan divergir por descuido — si algún día difieren, es un error, no una decisión.

**El interceptor sigue pendiente y es correcto que lo esté:** necesita `usuario_id`, `roles`, `ip` y `origen`, que salen del contexto de la petición autenticada. No se puede escribir antes que la auth, y la tabla sin interceptor no hace daño — simplemente no se llena todavía.

`archivo` conserva `eliminado_en` en lugar de pasar a un estado, a diferencia de `usuario`: aquí marca algo que un estado no daría, el momento en que dejó de existir el **binario** en el almacenamiento. La fila se queda para que el registro que lo referenciaba siga siendo legible, y no hay `DELETE`.

### Un hueco del trigger que se detectó al probarlo

El trigger append-only se escribió primero como `BEFORE UPDATE OR DELETE`. **Eso deja pasar `TRUNCATE`**: un trigger de `UPDATE`/`DELETE` no lo intercepta, así que `TRUNCATE auditoria` habría vaciado la bitácora entera sin despertarlo — precisamente en la tabla cuyo único propósito es ser inviolable.

Se corrigió a `BEFORE UPDATE OR DELETE OR TRUNCATE` **antes de que existiera ningún tenant**, revirtiendo y reaplicando las dos migraciones. Ese ciclo `Down`/`Up` sirvió además para comprobar que los `Down` limpian bien la función de plpgsql, que no se va sola al borrar la tabla.

La lección para las migraciones que vienen: un trigger de protección hay que probarlo con **todas** las sentencias que puede recibir, no solo con las obvias.

### Trampa de operación: dos instancias de la API a la vez

Costó un rato de diagnóstico y conviene dejarlo escrito. Con **dos procesos de
`Maquinaria.Api`** vivos —típicamente uno lanzado desde Visual Studio y otro con
`dotnet run`— pasan dos cosas:

1. **El build falla con `MSB3027`/`MSB3021`**, no por un error de código: el proceso vivo
   tiene tomadas las DLL y MSBuild no puede sobrescribirlas. Se distingue del error real
   porque el mensaje habla de *copiar* un archivo, no de compilarlo.
2. **La validación del JWT puede fallar con 401** aunque el token recién emitido sea
   válido. Se observó: `POST /api/empresas/{slug}/sesion` devolvía 200 y `GET
   /api/mi/sesion` devolvía `401` con un `WWW-Authenticate: Bearer` pelado, sin motivo.
   Con una sola instancia limpia, 200. No se determinó qué binario cargaba la instancia
   ajena, así que el mecanismo exacto queda sin confirmar.

**Antes de dar por roto algo, comprobar que hay una sola instancia:**

```powershell
Get-Process -Name Maquinaria.Api | Select-Object Id, StartTime
```

### Aprovisionamiento de empresas — 2026-08-21

`POST /api/plataforma/empresas`, protegido por la policy de plataforma. La secuencia de
siete pasos de [`05-esquema-fase0.md`](../05-esquema-fase0.md) §5, implementada.

**Probado de punta a punta contra Neon:** el alta de la empresa `demo` creó el tenant en
`Prueba`/`Lista` con su `version_esquema`, su suscripción al plan `base`, la base
`maquinaria_demo` con sus 10 tablas, `btree_gist` y `pg_trgm`, los 156 permisos y los 9
roles sembrados por las migraciones, el usuario `admin@demo.mx` en estado `Invitado` sin
hash, su asignación al rol `administrador` con `acceso_total`, y un `token_acceso` de
invitación vigente con hash SHA-256 de 64 caracteres y `creado_por_id` nulo — "la creó la
plataforma".

**Un defecto que se encontró probando, y que ninguna prueba unitaria habría visto.** Un
slug con formato inválido devolvía **500 en lugar de 400**: el formato del slug nunca se
validaba en la aplicación, lo atrapaba el `CHECK tenant_slug_formato` de la base, y como
el `SaveChanges` está fuera del `try`, salía como error genérico. Ahora hay `FormatoSlug`
en el dominio, con el mismo patrón que el `CHECK`.

**Es la misma lección que ya estaba escrita para `nombre_bd` y que no se había aplicado al
slug:** la base es la última línea de defensa, no la primera. Y ojo con confundir los dos
formatos — el slug **no admite guiones bajos**, solo guiones; el guion bajo aparece en
`nombre_bd`, que se deriva reemplazándolos.

**Se cerró también una carrera.** Entre `ExisteSlugAsync` y el `INSERT` cabían dos altas
simultáneas del mismo slug. El índice único es lo que de verdad lo impide; ahora el caso de
uso traduce la violación —`SQLSTATE 23505`— a un rechazo en lugar de a un 500. Reconocerla
exige saber de Npgsql, así que va detrás de `IRegistroTenants.EsColisionDeUnicidad` para
que Aplicación no dependa de infraestructura.

**Decisiones que quedaron tomadas:**

| tema | decisión |
|---|---|
| Proveedor de correo | **Resend**, con `HttpClient` tipado y sin paquete de NuGet. Ver [`01-arquitectura.md`](../01-arquitectura.md) §8.1 |
| Hash de tokens de un solo uso | **SHA-256, no PBKDF2.** Una contraseña es de baja entropía y hay que estirarla; un token de 256 bits de un CSPRNG no se adivina, así que estirarlo no agrega seguridad y sí 200 ms a cada apertura de liga |
| Vigencia de la invitación | 7 días, configurable. Un restablecimiento durará una hora |
| La liga en la respuesta HTTP | Solo en Development. En producción cualquiera con acceso al panel podría tomar la sesión del administrador de un cliente antes de que abra su correo |
| Aprovisionar en línea | Sí, como dice el diseño. `estado_aprovisionamiento` ya lo hace observable |

**Idempotencia, para que el reintento funcione.** `ExisteBaseAsync` consulta `pg_database`
antes del `CREATE` —PostgreSQL no tiene `CREATE DATABASE IF NOT EXISTS`—, `Migrate()` ya lo
es, y el sembrador no duplica el usuario ni deja dos invitaciones válidas circulando:
invalida las pendientes antes de emitir la nueva.

**Lo que falta de esta pieza:** ~~el endpoint que dispara el reintento~~ — **hecho el
2026-08-25**, ver la sección de abajo. Sigue faltando que `CorreoResend` se ejercite contra
la API real: hace falta la llave y un dominio verificado.

> **Al 2026-08-24:** `Correo:Proveedor` ya vale `"resend"`, así que el camino real es el que
> corre. Lo que sigue faltando es el secreto `Resend:Llave` y el dominio verificado — sin
> ellos el envío falla en silencio, porque es *best-effort* por diseño y solo deja rastro en
> el log. Ver [configuración](configuracion.md#resend).

### Resolución de conexión por empresa — 2026-08-21

La primera pieza del bloque que falta para cerrar la Fase 0. El diseño está en
[`01-arquitectura.md`](../01-arquitectura.md) §2.0; aquí van los hallazgos.

**La aplicación necesita la cadena DIRECTA en tiempo de ejecución, no solo para migrar.**
Hasta ahora `ConnectionStrings:Migraciones` era exclusiva de `dotnet ef`. Pero el
aprovisionamiento ejecuta `CREATE DATABASE`, que es DDL, y el endpoint *pooled* corre
PgBouncer en modo transacción y no lo admite. **Eso cambia el despliegue:** Railway tiene
que llevar las dos cadenas configuradas, no una.

**Un tenant sin suscripción vigente se queda sin ningún módulo.** Los módulos salen del
plan de la suscripción, así que el aprovisionamiento **tiene que crear la suscripción** o
la empresa arranca sin poder entrar a nada. Verificado contra la base: un tenant sin
suscripción devuelve 0 módulos. No es un defecto —es la consecuencia correcta— pero
convierte ese paso en obligatorio, no opcional.

**`ProveedorContextoEmpresa` construye contextos fuera del contenedor de DI**, para los
dos caminos que no tienen petición detrás: el aprovisionamiento, que migra una base que
acaba de crear, y `migrar-empresas`, que recorre todas. Consecuencia deliberada: esos
contextos **no llevan los interceptores registrados**, incluido el de auditoría. Es lo
correcto — migrar no es una operación de negocio auditable fila por fila, y el interceptor
necesitaría un usuario que en ese camino no existe.

**Se cerró un hueco que no estaba en ningún documento de diseño:** la lista de slugs
reservados. Un tenant con slug `plantilla` generaría `nombre_bd = maquinaria_plantilla` y
chocaría con la base de tiempo de diseño; igual `central`. Ahora vive en
`SlugsReservados`, junto con las bases que Postgres y Neon ya usan y los subdominios que
queremos para la plataforma.

**Verificado:** 44 pruebas en `Api.Tests`, incluidas las de inyección en el nombre de base
—`maquinaria"; DROP DATABASE postgres; --` y variantes— y la de que el mensaje de error no
repite el valor recibido. Y las tres consultas de resolución contra la base real, con un
tenant de prueba dentro de una transacción que termina en `ROLLBACK`: 26 módulos
contratados, el cupo propio de 300 equipos ganando sobre el valor por defecto, y la
compuerta respondiendo `true` para `rentas` y `false` para un módulo inexistente.

### Las 28 tablas de Fase 1, y dos cosas que solo salieron al probarlas — 2026-08-25 (noche)

Faltaba la mitad del entregable y no me di cuenta hasta que lo preguntaste: el esquema de
28 tablas vivia en [`06-esquema-fase1.sql`](../06-esquema-fase1.sql), que es un DOCUMENTO
DE DISENO, y **EF Core no lo lee**. Genera la migracion a partir de las entidades de C#, y
solo habia 10. Una tabla sin entidad no existe para EF y nunca llega a la base.

Cuando dijiste "actualiza las migraciones" te referias a las 28. Yo entregue 10.

Ahora estan las 18 que faltaban: `cliente`, `equipo`, `equipo_archivo`, `equipo_tarifa`,
`transferencia_equipo`, `ocupacion_equipo`, `cotizacion`, `cotizacion_linea`, `renta`,
`renta_linea`, `renta_concepto`, `extension_renta`, `contrato`, `contrato_clausula`,
`orden_compra`, `orden_compra_detalle`, `orden_venta`, `orden_venta_detalle`.

| | |
|---|---|
| tablas por base | **38** (28 de Fase 1 + 10 de Fase 0) |
| migraciones | 7 |
| CHECK / FK / PK | 62 / 59 / 39 |
| EXCLUDE | 2 |
| disparadores | 7 |
| huella de esquema (las tres bases) | `b4f101c3…` |

#### Las garantias se probaron, no se dieron por buenas

Que una restriccion exista no prueba que haga lo que promete. Se levantaron datos de prueba
en una transaccion y se intentaron 30 operaciones —cada una esperando ser aceptada o
rechazada— y al final ROLLBACK. **30 de 30.** Lo que quedo demostrado:

- **No se renta la misma maquina en fechas que se traslapan.** Traslape parcial, traslape
  de un solo dia, y una ocupacion abierta que empieza dentro de otra: los tres rechazados.
  Sin traslape, aceptado. Otro equipo en las mismas fechas, aceptado.
- **El mantenimiento compite por el calendario igual que una renta.** Por eso el traslape
  se controla en `ocupacion_equipo` y no en `renta_linea`: si se controlara en las lineas
  de renta, mandar una maquina al taller no impediria rentarla.
- **Cancelar libera el periodo** sin borrar la fila.
- **Bodega guarda, sucursal cotiza, patio las dos cosas.** Equipo en una sucursal:
  rechazado. Traspaso con destino sucursal: rechazado. Cotizar desde una bodega: rechazado.
- **Un contrato autorizado no se toca.** Editarlo, borrarlo, cambiar el texto de una
  clausula, agregar una clausula nueva o borrar una: los cinco rechazados. En borrador,
  todos aceptados.
- **Un solo precio vigente** por concepto, maquina y cliente.
- **Un flete se puede cotizar** sin equipo ni tipo de equipo — el caso que mi primera
  version del CHECK hacia imposible.

#### Dos fallos, y solo uno era del esquema

**El primero fue mi prueba.** Esperaba que una ocupacion abierta desde el 25 de septiembre
choncara con otra del 10 al 20, y no choca: `[25-sep, infinito)` no se cruza con
`[10-sep, 20-sep)`. La base tenia razon y la expectativa estaba mal. Se corrigio la prueba
para que empiece DENTRO del periodo existente, y ademas se agrego el caso que faltaba: una
ocupacion abierta bloquea todo lo posterior.

**El segundo si era real.** El diseno documentado declara `orden int NOT NULL DEFAULT 0` y
`cantidad int NOT NULL DEFAULT 1`, y la migracion salio SIN los DEFAULT: EF solo los emite
si la configuracion los pide. A traves de EF no se nota —la aplicacion siempre manda el
valor— pero cualquiera que inserte con SQL directo se topa con un NOT NULL sin defecto, y
sobre todo **el documento estaba mintiendo sobre la base**. Ya paso una vez con el
comentario del indice de `proveedor` que prometia trigramas sobre un btree. Se arreglo con
`EmpresaValoresPorDefectoRenglones`, cinco columnas en cuatro tablas.

Sin probar contra la base real, el primer fallo no habria existido y el segundo habria
quedado escondido hasta que alguien lo sufriera.

#### El documento de diseno se compara contra la base, no se supone

`06-esquema-fase1.sql` NO SE EJECUTA NUNCA —EF Core genera las migraciones desde las
entidades de C#—, asi que nada garantiza que siga describiendo lo que hay. Es exactamente
como se perdio la mitad del entregable.

Ahora hay un guion que parsea ese DDL y lo compara columna por columna contra
`information_schema`: **28 tablas, 307 columnas, cero desajustes**. Conviene volver a
correrlo cada vez que se toque el esquema.

De paso, dos numeros rancios que salieron de la auditoria: el `README.md` prometia
**31 tablas** en dos sitios —de antes de fusionar `cliente` con su contacto y su domicilio
y de quitar `obra`— y esta misma nota decia **11 tablas de Fase 0** cuando son 10; la
undecima es `__EFMigrationsHistory`.

#### Lo que sigue

El esquema esta completo y verificado, pero **no hay ni un endpoint** que use estas 18
tablas: no hay casos de uso, ni validaciones de aplicacion, ni pantallas. Eso es la
implementacion de Fase 1.

### Las cuatro bases al dia, y verificado contra la base real — 2026-08-25 (noche)

| base | migraciones | tablas |
|---|---|---|
| `maquinaria_central` | 4/4 | al dia desde antes |
| `maquinaria_plantilla` | 5/5 | recreada de cero |
| `maquinaria_demo` | 5/5 | via `migrar-empresas` |
| `maquinaria_bajio` | 5/5 | via `migrar-empresas` |

Lo que se comprobo, consultando las bases y no el codigo:

- **Las tres bases de empresa son identicas.** No "tienen las mismas tablas": dan la misma
  huella md5 sobre tabla+columna+tipo+nulabilidad+expresion generada — `d62325ad…`. Contar
  tablas no habria detectado una columna de tipo distinto en una sola base, que es
  precisamente el desfase que este comando existe para evitar.
- **`sucursal` ya no existe en ninguna.** Los tres tipos viven en `ubicacion`.
- **Las columnas generadas estan y con la expresion correcta:**
  `almacena_equipo = (tipo = ANY (ARRAY[1, 3]))`, `es_administrativa = (tipo = ANY (ARRAY[2, 3]))`.
- **Los indices parciales cuelgan de esas columnas** (`ix_ubicacion_almacena`,
  `ix_ubicacion_administrativa`), y el de `proveedor` es GIN de verdad — el que antes tenia
  un comentario que prometia trigramas sobre un btree.
- **18 restricciones CHECK, 15 FK, 21 PK** en cada base, iguales.
- **La central registro la version** de las dos empresas: `20260825162805_EmpresaCatalogosFase1`.

Sin `x` en `pg_constraint`: los dos `EXCLUDE` viven en `ocupacion_equipo` y `equipo_tarifa`,
tablas que todavia no existen. **De las 28 tablas del esquema de Fase 1 hay 10 aplicadas**
—las de catalogo y organizacion—; las 18 de operacion (cliente, equipo, renta, contrato,
ordenes) no tienen entidad en C# todavia. Eso es la implementacion de Fase 1.

### No era la credencial: era el `Database=` — 2026-08-25 (noche)

Durante horas dimos por bueno que el bloqueo era una credencial caducada de Neon. **No lo
era.** La credencial funcionaba; lo que estaba mal era a que base apuntaba:

| secreto | apuntaba a | debia apuntar a |
|---|---|---|
| `ConnectionStrings:Central` | `maquinaria_plantilla` | `maquinaria_central` |
| `ConnectionStrings:Migraciones` | `maquinaria_plantilla` | `maquinaria_central` |

El sintoma era `relation "tenant" does not exist`, y antes de eso un `28P01` que si fue una
credencial vencida — dos fallos distintos encadenados, y el segundo se leyo como si fuera
el primero.

**Lo que casi sale caro:** con `Migraciones` apuntando a la plantilla, un
`dotnet ef database update --context ContextoCentral` habria creado las tablas de la base
CENTRAL dentro de `maquinaria_plantilla`. Se evito por revisar antes de escribir.

La leccion no es "revisa la cadena": es que **un mensaje de error de conexion no dice a que
base te conectaste**. Conviene mirarlo antes de aplicar DDL, no despues.

#### Estado real de las cuatro bases

Nada se habia perdido:

| base | migraciones | situacion |
|---|---|---|
| `maquinaria_central` | 4/4 | al dia |
| `maquinaria_plantilla` | 4 + `EmpresaCatalogosOrganizacion` | fila de una migracion que **ya no existe en disco** |
| `maquinaria_demo` | 4 | le falta `EmpresaCatalogosFase1` |
| `maquinaria_bajio` | 4 | le falta `EmpresaCatalogosFase1` |

`maquinaria_plantilla` conserva `sucursal` —tabla que el diseno actual ya no tiene, porque
los tres tipos viven en `ubicacion`— y las tablas de una version descartada del catalogo.
Aplicar `EmpresaCatalogosFase1` encima falla: `categoria_equipo`, `marca` y compania ya
existen. **Se recrea, no se migra.** Es una base desechable: el aprovisionamiento NO la usa
como plantilla de `CREATE DATABASE` —crea la base vacia y corre migraciones—, asi que
soltarla no afecta a ningun cliente.

### `migrar-empresas` y dos defectos propios — 2026-08-25 (noche)

Dos cosas que solo se vieron al correrlo de verdad:

**`RevisarAsync` nombraba `maquinaria_plantilla`** para construir un contexto y preguntarle
al ensamblado que migraciones existen. `GetMigrations()` lee el ENSAMBLADO, no la base, asi
que la conexion nunca se abria — pero ataba la revision de esquema a que una base
desechable siguiera existiendo. Ahora hay `ParaLeerMigraciones()`, que no nombra ninguna.

**El comando volcaba la pila** justo debajo del mensaje limpio, deshaciendo el trabajo de
tenerlo limpio. En un comando de consola la consola ES el log: la pila se pide con
`--detalle`.

El comando existe y llega hasta la base; lo único que lo detiene es la credencial. Diseño
en [`06-alcance-fase1.md`](../06-alcance-fase1.md) §8.

**Un defecto que salió al primer intento de correrlo.** El comando no arrancaba:

```
Correo:Proveedor es 'resend' pero falta Resend:Llave. Va en secretos.
```

Era mi propia comprobación de arranque haciendo su trabajo en el lugar equivocado.
Validaba **al registrar los servicios**, así que `migrar-empresas` —un comando que no manda
ni un correo— no podía arrancar sin configurar el proveedor de correo.

**Registrar servicios no debe validar lo que ese arranque en concreto no va a usar.** La
validación se movió al constructor de `CorreoResend`, que se construye la primera vez que
alguien intenta enviar: el fallo sigue siendo temprano y claro, y cada camino solo exige lo
que necesita. Al registrar queda un aviso en `stderr` para no perder la señal.

Es la segunda vez que un "fallo rápido" bien intencionado bloquea un camino que no le
correspondía. La regla que queda: **el arranque valida lo que ese arranque usa.**

**Y el comando fallaba con un volcado de pila** cuando no podía leer la lista de empresas
—el `try` estaba dentro del bucle por empresa, y eso pasa antes—. Ahora da un mensaje de
una línea, la pista de dónde mirar, y código de salida `2`.

### Fase 1 desbloqueada, y tres garantías más en el motor — 2026-08-25

**El primer entregable no calcula precios: los captura.** Decisión del negocio, y es lo que
desbloquea la fase. Las ocho preguntas de tarificación de
[`04-pendientes.md`](../04-pendientes.md) §1.2 quedan **abiertas pero ya no bloqueantes**,
porque eran todas sobre reglas de cálculo. El sistema multiplica cantidad por precio y suma
líneas; no escoge tarifas, no prorratea extensiones, no calcula horas excedentes.

Lo que sí hay que hacer bien desde ahora: **congelar el precio aplicado en cada línea**. Si
mañana se automatiza el cálculo, los documentos viejos tienen que seguir mostrando lo que se
cobró.

**`ubicacion` gana dos columnas generadas**, y eso convierte una convención del código en
una garantía del motor:

```sql
almacena_equipo    GENERATED ALWAYS AS (tipo IN (1, 3)) STORED
es_administrativa  GENERATED ALWAYS AS (tipo IN (2, 3)) STORED
```

El detonante fue *"se deben permitir traspasos de equipos entre bodegas y patios"*. Con
banderas capturadas, mantenerlas en sincronía con el tipo sería trabajo de la aplicación y
tarde o temprano una se queda atrás. Generadas, una "bodega que cotiza" **no se puede
escribir**. Y existen en la base —no solo como propiedad derivada en C#— para que las tres
reglas que cruzan tablas se apoyen en ellas: equipo solo donde se almacene, traspaso solo
entre ubicaciones que almacenen, cotización solo desde una administrativa. Las hará cumplir
un trigger cuando existan esas tablas.

**El contrato es inmutable tras autorizarse.** Estados `Borrador → Autorizado → Terminado`
más `Cancelado`, y un trigger que rechaza `UPDATE`/`DELETE` cuando el estado ya no es
borrador — mismo patrón que `rol_sistema_inmutable`. Es un documento con firmas: si se
pudiera cambiar el texto después, la firma no significaría nada. Cambiarlo exige cancelarlo
y hacer otro.

**Las cláusulas vienen de dos orígenes**, y por eso `contrato_clausula.clausula_id` es
nullable: del catálogo general, o redactadas para ese cliente. En el segundo caso el texto
del contrato es el único origen. Y en los dos casos el texto se **copia**, no se
referencia — corregir la plantilla no cambia contratos ya firmados.

**`motivo = Venta` en `ocupacion_equipo`.** Al finalizar una orden de venta el equipo cierra
su calendario, y deja de poder rentarse **sin que el módulo de rentas sepa nada de ventas**.
Es el punto de unión entre las dos cosas.

### La migración de Fase 1 se reescribió tres veces, a propósito

`EmpresaCatalogosFase1` es una sola migración con **10 tablas**, y llegó ahí después de
quitarse y regenerarse tres veces: por la corrección de ubicación, por `tarifa` y por
`clausula`. Se pudo hacer porque **nunca se aplicó a `demo` ni a `bajio`** — solo a
`maquinaria_plantilla`, que es desechable por diseño.

Reescribir en lugar de encimar correcciones deja una historia legible. La regla *append-only*
aplica a lo que ya llegó a la base de un cliente, no a lo que todavía no sale del
repositorio.

**Consecuencia operativa:** hay que **borrar y recrear `maquinaria_plantilla`**, no
migrarla. Conserva la tabla `sucursal`, la `ubicacion` vieja y filas de
`__EFMigrationsHistory` de versiones que ya no existen.

### El alcance de la Fase 1 se consolidó en un documento

Estaba repartido en tres archivos con enmiendas fechadas encima de enmiendas, y ya no se
podía leer de corrido. Ahora vive en
[`06-alcance-fase1.md`](../06-alcance-fase1.md), que **manda** sobre el alcance del primer
entregable e incluye las decisiones **con su historial** — cuáles se cerraron, se
revirtieron y se volvieron a tomar. Sin ese historial, alguien va a "corregir" el modelo de
vuelta a una versión descartada.

### Cambios de alcance y una corrección de modelo — 2026-08-24 (tarde)

El negocio revisó el alcance del primer entregable. **Dos decisiones que se habían cerrado
el mismo día se revirtieron**, y conviene que quede el rastro:

| se había cerrado como | quedó en |
|---|---|
| la renta **no** incluye operador (§2.4) | **sí** puede incluirlo — solo quién va y cuánto se cobra |
| venta y compra **fuera** del primer entregable | **dentro**, con proceso corto |

**Corrección de modelo: `sucursal` desaparece como entidad.** Yo había construido una
jerarquía `sucursal → ubicacion`, y el negocio las define como **tres tipos de sitio al
mismo nivel**:

```
bodega     guarda maquinas
sucursal   administra y cotiza
patio      las dos cosas
```

Una sola tabla `ubicacion` con `tipo`, y las dos capacidades —`AlmacenaEquipo`,
`EsAdministrativa`— **derivadas** del tipo en lugar de guardadas como banderas. Con
banderas se podría crear una "bodega que cotiza", que no existe; derivándolas es imposible
de escribir.

Consecuencia que hay que hacer cumplir en el dominio, porque cruza dos tablas y ningún
`CHECK` la alcanza: **un equipo solo puede estar en una ubicación que almacene**, y **una
cotización solo puede salir de una administrativa**.

**Las tarifas son un catálogo de conceptos cobrables**, no el precio por periodo de un
equipo. Renta diaria, mantenimiento, flete, operador, maniobras: cada una es una fila con
su `unidad` —hora, día, semana, mes, evento, kilómetro— y con dónde aplica, renta o venta.
Una renta o una venta arrastra **varias**.

Eso unifica tres cosas que si no tendrían tabla cada una: el flete se cotiza sobre la renta
como línea con tarifa de flete; el operador, como línea con tarifa de operador más el
trabajador que va; el mantenimiento, igual. El **precio** no vive en el catálogo — vive por
equipo y con vigencia, en `equipo_tarifa`, que es del bloque siguiente.

**Duda de plan que hay que resolver:** el límite `max_sucursales` del plan contratado ahora
no tiene una tabla `sucursal` que contar. ¿Cuenta todas las ubicaciones, o solo las de tipo
`Sucursal`? Cuento todas mientras nadie diga lo contrario — es lo que escala con el tamaño
de la empresa — pero hay que confirmarlo.

### La migración de catálogos se reescribió, y la plantilla quedó desalineada

`EmpresaCatalogosOrganizacion` se **quitó y se regeneró** como
`EmpresaCatalogosOrganizacionTarifas`, con la ubicación corregida y la tabla `tarifa`. Se
pudo reescribir en lugar de encimar una corrección porque **nunca llegó a `demo` ni a
`bajio`**: solo se había aplicado a `maquinaria_plantilla`, que es desechable por diseño.

**Pero eso deja la plantilla inconsistente:** tiene la tabla `sucursal`, la `ubicacion`
vieja, y una fila en `__EFMigrationsHistory` de una migración que ya no existe. En cuanto
haya credencial hay que **borrar y recrear `maquinaria_plantilla`** — no intentar migrarla.

### No se pudo aplicar ni verificar nada

La credencial de Neon en `user-secrets` devuelve `28P01: password authentication failed for
user 'neondb_owner'`. El código compila y la migración se genera —EF no necesita la base
para eso— pero **nada de esto está aplicado ni probado contra la base real**, que es como se
ha verificado todo lo demás en este proyecto. Queda pendiente en cuanto vuelva la cadena.

### Fase 1, bloque A: catálogos, ubicación y trabajadores — 2026-08-24

Nueve tablas aplicadas a `maquinaria_plantilla`, que pasa de 10 a 19:

```
categoria_equipo · tipo_equipo · marca · modelo_equipo
sucursal · ubicacion · puesto · trabajador
proveedor
```

Son las que **no dependen de las reglas de tarificación**, que siguen siendo el bloqueante
del cierre de la Fase 1.

**Trabajador y usuario son entidades distintas, y esa es la decisión de fondo.** Un
trabajador es una persona con un puesto; un usuario es una cuenta con roles. La mayoría del
personal de patio no entra al sistema, y el administrador de la empresa podría no ser
trabajador. La liga es opcional en los dos sentidos: `trabajador.usuario_id` nullable con
**único parcial**. Se puso la FK de ese lado a propósito, para **no tocar la tabla
`usuario`**, que es de la Fase 0 y ya está migrada en las bases existentes.

**`ubicacion` en lugar de `patio`.** El documento solo dice "patios", pero el negocio tiene
bodegas y talleres. Un `tipo` —`Patio | Bodega | Taller | Otro`— cubre los tres y los que
falten sin inventar una tabla por cada uno. Guardar una bodega en una tabla llamada `patio`
se lee mal y envejece peor.

**Un constraint que vale la pena señalar:** `trabajador_baja_coherente` exige
`(estado = Baja) = (fecha_baja IS NOT NULL)`. Sin él, "de baja sin fecha" y "con fecha pero
activo" son indistinguibles de los datos buenos, y el día que alguien filtre por
`fecha_baja` los números mienten en silencio.

**Verificado contra la base real:** 13 casos negativos rechazados —tipo fuera de rango,
media coordenada, latitud imposible, código repetido en la misma sucursal, las dos
incoherencias de baja, número de empleado duplicado, borrar un puesto en uso, borrar una
sucursal con ubicaciones, modelo duplicado de la misma marca— y 3 positivos aceptados,
entre ellos el mismo código de ubicación en **otra** sucursal y dos trabajadores sin cuenta,
que es lo que prueba que el único es parcial. Todo dentro de una transacción que termina en
`ROLLBACK`.

**Un comentario que mentía, corregido antes de aplicar.** El índice de `proveedor` decía
servir para búsqueda por texto parcial con `pg_trgm`, pero era un btree, que solo acelera
igualdad y prefijos — no `%excavadora%`. Ahora es
`USING gin (razon_social gin_trgm_ops)`, verificado en `pg_indexes`.

### El desfase de esquema dejó de ser teórico

`maquinaria_plantilla` está en `EmpresaCatalogosOrganizacion` y **`demo` y `bajio` siguen en
`EmpresaPermisosModulosCompletos`**. Las dos empresas reales están una migración atrás.

Es exactamente el escenario para el que existe `migrar-empresas`.

> **Al 2026-08-25:** el comando que aquí se echaba en falta **ya está escrito** —ver la
> sección de abajo— y con él el endpoint que hace visible el desfase. Lo que no ha cambiado
> es el desfase en sí: `demo` y `bajio` **siguen una migración atrás** hasta que alguien
> corra el comando contra Neon. Escribir la herramienta y usarla son dos cosas distintas, y
> confundirlas es justo el tipo de mentira que esta bitácora existe para no contar.

### Alcance del primer entregable: se evaluó ampliarlo a venta y compra — 2026-08-21

Se planteó incluir **venta, compra y renta** de equipos en el primer entregable. Se
analizó y **se acotó de vuelta a rentas**.

El análisis vale conservarlo, porque el riesgo no era obvio: *"venta"* significa dos cosas
con modelos incompatibles.

| lectura | qué implica |
|---|---|
| Vender equipo **usado del parque** | Desinversión de un activo serializado. Barato: un motivo de baja y un documento de venta |
| Vender equipo **nuevo comprado para revender** | Inventario con existencias y movimientos. **Adelanta M16 y M17 de la Fase 3 a la Fase 1** |

La segunda no es ampliar el MVP, es duplicarlo.

**La decisión que sí se tomó, y es la que importa:** el equipo tiene **un solo ciclo de
vida** que puede terminar en venta. No hay parques separados. Eso descarta la alternativa
cara —decidir al comprar si una máquina es de renta o de venta, y no poder cambiarlo— y
deja la venta como algo aditivo sobre el modelo actual. Anotado en
[`02-modelo-datos.md`](../02-modelo-datos.md).

**Lo que NO se hizo, a propósito:** no se agregó un `tipo` a `cotizacion_linea` "por si
acaso". Agregarlo después es `ADD COLUMN tipo smallint NOT NULL DEFAULT 1` con backfill
implícito — trivial. Adelantar estructura para una funcionalidad que no está decidida es
como se llenan los modelos de columnas que nadie usa.

### La especificación funcional entró al repositorio — 2026-08-21

El `.docx` que `docs/README.md` enlazaba y no existía ya está versionado, junto con su
texto extraído en [`especificacion-funcional.md`](../especificacion-funcional.md) para
poder leerlo, buscarlo y citarlo sin abrir Word.

Con el documento a la vista se corrigieron tres cosas que la documentación del proyecto
tenía mal:

**1. Son 26 módulos, no 30.** El documento numera hasta 30 pero **salta el 21, 22, 23 y
28**: esos módulos no existen. No faltaban de nuestra lectura, faltan del documento. La
cifra "30 módulos" estaba en siete archivos y ya está corregida.

**2. Cuatro módulos estaban mal nombrados.** Se habían inferido de los documentos de
diseño, y el documento los define distinto:

| # | se había sembrado | el documento dice |
|---|---|---|
| 24 | `configuracion` — "Configuración" | **Sucursales y patios** |
| 25 | `seguridad` — "Seguridad" | **Usuarios y permisos** |
| 27 | `rentabilidad` — "Rentabilidad y reportes" | **Reportes** |
| 29 | `campo` — "Campo" | **QR de equipos** |

El de M29 era el error real: se asumió que era la PWA de campo porque la Fase 5 se llama
así, y M29 es específicamente el QR del equipo. **La PWA es una fase, no un módulo.**

**3. Se completó el catálogo, en dos migraciones coordinadas.** `CentralModulosCompletos`
renombra los cuatro y agrega los ocho que faltaban —M9, M10 (inspecciones de salida y
devolución), M13 a M18 (taller)—, y `EmpresaPermisosModulosCompletos` hace lo propio en la
base de empresa: renombra los 24 permisos afectados y siembra 48 nuevos. El catálogo pasó
de **108 a 156 permisos**: 26 módulos × 6 acciones.

**Las dos migraciones van juntas, siempre.** `permiso.modulo` referencia `modulo.clave` de
la base central y no puede tener FK, porque son bases distintas. Aplicar solo una deja la
compuerta de autorización —`permisos del rol ∩ módulos del plan`— sin cerrar, en silencio.
Esto es exactamente lo que la prueba de CI pendiente tiene que detectar, y este cambio es
la prueba de que el riesgo es real y no teórico.

Verificado contra las dos bases: 26 módulos en la central, 156 permisos sobre 26 módulos
en `maquinaria_plantilla`, y **cero filas con las claves viejas**.

### El primer entregable, definido con el documento

El documento fija la Fase 1 así: *«usuarios, roles, equipos, clientes, tarifas,
disponibilidad, cotizaciones y rentas»*. Usuarios y roles ya están; queda **M2, M3, M4,
M5, M7 y M24**, que es lo que el plan del proyecto ya tenía.

Sobre el **principio de integración** del documento —*«los módulos no deberán funcionar de
manera aislada»*— se consideró y se descartó diseñar el núcleo con independencia estricta
entre módulos. Las tablas del núcleo se relacionan entre sí con FK reales; lo que sigue
valiendo es que **los módulos de fases posteriores apuntan hacia el núcleo** —evidencia
polimórfica, `ocupacion_equipo` con `motivo` y `referencia_id`, `movimiento_costo` que
nadie referencia—, porque eso es lo que permite construir por rebanadas sin reescribir.

### Login de plataforma — la primera rebanada vertical completa

`POST /api/plataforma/sesion` y `GET /api/plataforma/sesion/actual`, con hashing, JWT, manejo de errores, health check, limitador de intentos y siembra del primer superadministrador. Las decisiones de autenticación están en [`01-arquitectura.md`](../01-arquitectura.md) §6.1.

**Se reordenaron dos pasos del plan de arranque, por razones concretas:**

1. **El paso 11 —resolución de conexión por empresa— es prerrequisito del 9, no su sucesor.** Aprovisionar *es* crear una base y correrle las migraciones de `ContextoEmpresa`, y eso exige construir un `ContextoEmpresa` contra un nombre de base arbitrario en tiempo de ejecución. Hoy solo existe la fábrica de tiempo de diseño, clavada a `maquinaria_plantilla`.
2. **El login de plataforma va antes que el aprovisionamiento**, porque el endpoint que da de alta una empresa tiene que estar protegido. Ningún documento describía este login: §6 especifica el ingreso de los usuarios de empresa —`empresa / correo / contraseña`— pero el superadministrador vive en la central y no tiene slug. Son **dos flujos de autenticación distintos** y solo uno estaba diseñado.

**Cómo se crea el primer superadministrador.** No hay registro público en ninguna parte, así que sin un arranque no habría con qué iniciar sesión nunca. Se lee de `Arranque:Superadmin` —secretos en desarrollo, variables de entorno en Railway— y **solo actúa si la tabla está vacía**, así que no es una puerta trasera: en cuanto existe un superadministrador, ese código no puede crear otro ni pisar el que hay. No se hizo como semilla en migración a propósito: una migración lleva su contenido en el historial para siempre, y la contraseña quedaría en el repositorio.

**Un 401 que parecía de autorización y no lo era.** El endpoint protegido rechazaba tokens válidos. La causa: JwtBearer, por defecto, traduce los claims estándar entrantes a los URIs de WS-Federation, así que el token decía `sub` y el código lo buscaba por ese nombre sin encontrarlo — la autorización sí pasaba, y el 401 lo devolvía el propio handler al no poder leer la identidad. Se corrige con `MapInboundClaims = false`, que además es lo coherente habiendo emitido nombres cortos.

**Lo que queda a medias, dicho explícitamente:**

- **El límite de intentos es solo por IP.** El limitador nativo corre antes de leer el cuerpo de la petición, así que no puede particionar por correo. El límite por combinación de correo —y de slug, cuando exista el login de empresa— necesita estado y va en el caso de uso.
- **La plataforma no tiene refresh token.** La base central no tiene tabla `sesion_refresh`, así que un superadministrador con token vencido vuelve a iniciar sesión. Por eso su vigencia es de 60 minutos y no de 15. Agregar refresh para plataforma es una decisión de esquema pendiente.
- **La policy de ámbito de empresa no existe todavía**, solo la de plataforma. La audiencia ya está reservada en la configuración.

### Cómo se prueba una tabla append-only

La prueba de `auditoria` corre entera dentro de una transacción que termina en `ROLLBACK`, y no por comodidad: la tabla no se puede limpiar. **La única forma de que una fila de auditoría no exista es no confirmarla nunca**, así que el `ROLLBACK` es en sí mismo parte de la demostración. Está en el guion de pruebas junto a los 12 casos negativos.

### CORS por subdominio y acceso desde el navegador — 2026-08-24

Hasta aquí la API se había probado con `curl` y PowerShell. La primera llamada real desde
Angular destapó dos cosas distintas que se manifestaban igual, y una de ellas obligó a
cambiar el modelo de orígenes.

**La lista fija de orígenes dejó de servir, porque cada empresa vive en su propio
subdominio.** El conjunto es abierto: crece con cada cliente. Mantenerlo en configuración
significaría redesplegar la API cada vez que se da de alta una empresa — un despliegue por
venta. Ahora hay un predicado en
[`OrigenesPermitidos`](../../src/Maquinaria.Api/Arranque/OrigenesPermitidos.cs), con dos
claves nuevas: `Cors:DominioBase` y `Cors:ExigirHttps`.

Se mantiene también una lista de orígenes **exactos**. Ahí van los de la plataforma —el
panel de superadmin y la pantalla de selección de empresa—, que no son subdominios de
cliente. La lista exacta gana y no pasa por ninguna validación de forma: es configuración
nuestra, no entrada del exterior.

**Por qué un predicado y no `AllowAnyOrigin`.** `AllowAnyOrigin` deshabilita las
credenciales y deja que cualquier sitio llame a la API desde el navegador de un usuario con
sesión abierta. `SetIsOriginAllowed` sí es compatible con `AllowCredentials`, que hará falta
cuando el token de refresco pase a cookie `HttpOnly`.

**El punto del prefijo es lo que hace segura la comparación.** Se acepta el dominio pelado
—ahí vive la pantalla que pregunta a qué empresa entras— y cualquier cosa bajo él, pero
comparando contra `"." + dominio`: `malo-ejemplo.com` termina en `-ejemplo.com`, no en
`.ejemplo.com`, así que no pasa. Un `EndsWith("ejemplo.com")` sin el punto regalaría el CORS
a un dominio ajeno, y es exactamente el error que uno escribe sin pensarlo.

**Lo que esta comprobación NO hace, a propósito: verificar que el subdominio sea una empresa
real.** Serían dos costos, y el segundo es el que decide:

1. Una consulta a la base **en cada preflight**, es decir antes de cada petición no trivial
   del navegador.
2. Un enumerador de clientes. Aceptar `bajio.ejemplo.com` y rechazar `otro.ejemplo.com`
   dice cuáles slugs son clientes — justo lo que evitan las reglas anti-enumeración del
   login y del restablecimiento. Sería tirar por la ventana la defensa que costó el piso de
   tiempo constante.

Que el tenant exista lo resuelve la petición, no el CORS. Hay una prueba dedicada a fijar
esa decisión: `Un_subdominio_inexistente_se_acepta_a_proposito`.

**En desarrollo `DominioBase` vale `localhost`**, que habilita `bajio.localhost:4200` **sin
tocar el archivo `hosts`**: Chrome y Edge resuelven `*.localhost` a `127.0.0.1` de forma
nativa. Y `ExigirHttps` se apaga solo ahí, porque el dev server de Angular es http.

**`UseHttpsRedirection` rompía todas las llamadas del navegador**, y el síntoma engañaba
tanto que se registró aparte, en [trampas conocidas](trampas-conocidas.md#usehttpsredirection-rompe-todas-las-llamadas-del-navegador-en-desarrollo).
En corto: el preflight pasaba con 204 y la petición real moría en
`ERR_CERT_AUTHORITY_INVALID` al redirigirse al puerto https, mientras `curl` y PowerShell
—que no validan el certificado igual— confirmaban que la API estaba sana. Se decidió no
redirigir en desarrollo; en producción la redirección sigue activa.

**Se reservó el slug `login`.** Con el subdominio como identificador de tenant, ahí vive la
pantalla que pregunta a qué empresa se entra. Dejarlo libre permitiría que una empresa se
quedara con la puerta de entrada de todas las demás. Va en `SlugsReservados`, junto a
`central` y `plantilla`.

**`InternalsVisibleTo` en `Maquinaria.Api.csproj`.** La decisión de qué origen se acepta es
lógica de seguridad y tiene que poder probarse, pero `OpcionesCors` y `OrigenesPermitidos`
son detalle del arranque de la Api. Se abre el assembly a `Maquinaria.Api.Tests` en lugar de
volver públicos tipos que nadie fuera de la Api tiene por qué ver — es el compromiso más
barato de los dos.

**Verificado:** 9 métodos de prueba en `OrigenesPermitidosPruebas`, **22 casos** contando
los `InlineData`, corridos el 2026-08-24. Cubren los subdominios válidos, los dominios
ajenos que se le parecen, http contra https según `ExigirHttps`, esquemas que no son web,
orígenes no absolutos, la lista exacta sola y conviviendo con el dominio base, y el
subdominio inexistente que se acepta a propósito.

**Resend quedó activado**: `Correo:Proveedor` pasó de `"log"` a `"resend"` en
`appsettings.json`. Sigue faltando `Resend:Llave` y un dominio verificado; mientras tanto la
cuenta está en sandbox y solo entrega al correo del titular. Ver
[configuración](configuracion.md#resend).

**Un cabo suelto menor:** el `appsettings.json` base trae `Cors:Origenes:
["*.localhost:4200"]`, y la lista exacta se compara con `Contains`. Un comodín ahí no
coincide con nada y además no es un origen válido —le falta el esquema—. En desarrollo no se
nota porque `appsettings.Development.json` sustituye la lista entera. Es configuración
muerta que conviene limpiar.

### Restablecimiento de contraseña de usuarios de empresa — 2026-08-24

`POST /api/empresas/{slug}/restablecimientos` para pedir la liga, `GET .../{token}` para
saber si sirve y `POST .../{token}` para definir la contraseña. No hizo falta migrar nada:
`token_acceso` y `PropositoToken.RestablecerContrasena` ya lo anticipaban.

**La respuesta de la solicitud es idéntica exista o no el correo, y exista o no la
empresa.** Un formulario de recuperación se llena sin sesión y admite cualquier dirección,
así que cualquier diferencia lo convierte en un enumerador de la lista de empleados de un
cliente —y probando slugs, de la lista de clientes—. Se garantiza en tres capas:

1. **El caso de uso no devuelve nada.** `SolicitarRestablecimiento.EjecutarAsync` es
   `Task`, no `Task<algo>`: el endpoint no tiene sobre qué ramificar aunque alguien lo
   intente más adelante. El cuerpo del 202 es una instancia estática única.
2. **Tiempo constante por piso, no solo por señuelo.** El hash señuelo de
   `IniciarSesionEmpresa` no alcanza aquí: imita el costo de un PBKDF2, no el de dos
   escrituras y un POST a Resend. Se responde siempre al cumplirse un piso fijo de 1200 ms,
   rellenando con espera lo que sobre, y el envío de correo va acotado a 800 ms —por debajo
   del piso— para que el relleno nunca sea cero. El señuelo se conserva porque es la
   defensa que no depende de que el piso esté bien dimensionado.
3. **Las excepciones se tragan y se registran.** Una que subiera sería un 500, y un 500 que
   solo aparece cuando la cuenta existe delata igual que un mensaje distinto.

**El límite conocido de la defensa, dicho explícitamente:** si la base se degrada por
encima del piso, el relleno se agota y la diferencia vuelve a ser medible.

**Al cambiar la contraseña se revocan todas las sesiones de refresco**, dentro de la misma
transacción que la guarda y quema el token. Si alguien restablece porque le tomaron la
cuenta y las sesiones del atacante siguen vivas, el restablecimiento no sirvió de nada.

**La regla de vigencia de un token se escribe una sola vez.** `TokenAcceso.Vigente` es una
`Expression`, no un método: EF Core la traduce a SQL y las pruebas la compilan y la
ejecutan sin base de datos. Dos copias de una regla de seguridad es una copia que se queda
atrás — y la parte que un copy-paste del flujo de invitación olvidaría es justo el filtro
por propósito, que es lo que impide que una liga de invitación cambie la contraseña de una
cuenta activa.

**El `GET` no revela nada.** El de invitación devuelve a quién va dirigida la liga porque
es el primer contacto y la pantalla tiene que decirlo; el de restablecimiento devuelve 204
o 404 y nada más. Quien restablece ya conoce su cuenta, así que mostrar el correo solo
convertiría una liga adivinada en una confirmación de que esa dirección existe en esa
empresa.

**Límite de intentos propio**, más estricto que el del grupo: 3 cada 15 minutos por slug e
IP, contra 10 por minuto. Es el único endpoint anónimo que manda correo, y eso cambia a
quién le cuesta el abuso — le llega al buzón de un tercero y gasta cuota de Resend.

**La vigencia de una hora no es configurable**, a diferencia de los días de la invitación.
Los días son comodidad operativa; la ventana del restablecimiento es el tiempo durante el
cual un correo interceptado abre una cuenta ajena, y dejarla en un `appsettings` es dejar
que alguien la suba a treinta días sin darse cuenta. Vive en
`PoliticaRestablecimiento`, en el dominio.

**El texto de la vigencia vive junto al número.** `PoliticaRestablecimiento.VigenciaTexto`
es lo que la plantilla del correo interpola, así que cambiar una hora obliga a ver la otra.
Una plantilla que promete un plazo distinto del que aplica no rompe nada: genera tickets de
soporte, que es peor, porque nadie los relaciona con un cambio de código.

**La liga cambió de forma: el slug va en el HOST, no en la cadena de consulta.**
`PlantillasCorreoWeb` construye `bajio.<dominio>/invitacion?token=…` en lugar del
`?empresa=bajio` anterior, y aplica igual a la de restablecimiento (`/restablecer`). Es
consecuencia directa del CORS por subdominio: cada empresa vive en el suyo y es de ahí de
donde el frontend saca a qué empresa se entra, así que una liga al dominio pelado llegaría a
un sitio donde esas pantallas no existen.

Se arma con `UriBuilder` y no concatenando: el esquema y el puerto salen de
`Correo:UrlBaseAplicacion` sin tener que interpretarlos, y en desarrollo
`http://localhost:4200` da `http://bajio.localhost:4200` sin ningún caso especial.

**Verificado el 2026-08-24: 29 pruebas nuevas** —9 en `VigenciaDeTokenPruebas` y 20 en
`RestablecimientoPruebas`, las dos clases dentro de `RestablecimientoPruebas.cs`—, y ese día
`Maquinaria.Api.Tests` quedó en **116 en total, todas en verde**.

> Ese 116 es el total **de ese día** y ya no es el actual. El reparto vigente, con las 205
> del 2026-08-25, está en [las cifras corregidas](#cifras-que-la-propia-bitácora-tenía-mal).
> Se deja el número histórico y no se pisa: la bitácora registra cuándo se midió qué, y un
> total sin fecha no vale nada.

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

Los documentos de [`docs/`](../) son especificación, no inventario. Diferencias reverificadas contra el disco el **2026-08-25**:

| Documento dice | Realidad |
|---|---|
| Repos `maquinaria_back` y `maquinaria_front` | `maquinaria-backend` y `maquinaria-frontend` |
| Contenedor en `Documents/Maquinaria/` | `OneDrive/Desktop/maquinaria/` |
| Frontend en Angular 22 / CLI 22.1.4 | `@angular/core` `^21.2.0`, `@angular/cli` `^21.2.21` |
| Checklist marca el andamiaje del backend como hecho | Se creó el 2026-08-20 |
| `03-plan-desarrollo.md` §4 pone `migrar-empresas` como paso 10, entre las dos migraciones y el aprovisionamiento | Se escribió **después** del aprovisionamiento y del login de empresa, el 2026-08-25. El orden real fue 7 → 8 → 11 (conexión) → login → 9 → 12 → 10 |
| `03-plan-desarrollo.md` §4 mete el interceptor de auditoría dentro del paso 11 | La resolución de conexión se cerró el 2026-08-21; el interceptor sigue pendiente. Son dos piezas con dependencias distintas y el documento las cuenta como una |

Verifica siempre contra el repo antes de asumir que algo está hecho. **Cuando el documento y el código no coinciden, gana el código.**

Las dos filas de `03-plan-desarrollo.md` se dejan como divergencia y no se corrigen en su
documento a propósito: ese documento es **plan**, y el plan se cumplió en otro orden por
razones que ya están registradas —el paso 11 resultó prerrequisito del 9, y el login de
plataforma tuvo que ir antes del aprovisionamiento para poder protegerlo—. Reescribir el plan
para que coincida con lo que pasó borraría la información de que el orden cambió y por qué.

**Tres afirmaciones de `guias/convenciones.md` sí se corrigieron en su propio documento** el
2026-08-25, porque ahí no eran plan sino descripción del sistema:

1. **La auditoría con su `SaveChangesInterceptor`** figuraba como convención vigente. Las dos
   tablas `auditoria` existen; el interceptor no. Queda marcado como la única fila de esa
   tabla que describe algo que todavía no corre.
2. **El refresh token en cookie `HttpOnly`.** Va **en el cuerpo JSON**, en el login y en el
   refresco, y no hay ni una cookie en la API. El CORS ya está preparado para el cambio
   —`SetIsOriginAllowed` en lugar de `AllowAnyOrigin`, justo para poder habilitar
   `AllowCredentials`— pero el cambio no se ha hecho, así que mientras tanto lo que protege el
   token es la rotación con detección de reuso y no el navegador.
3. **`IAlmacenamientoArchivos` como algo que "existe por esto".** Solo se lo cita en
   comentarios de otros archivos: no hay interfaz ni implementación. Y de paso, la misma regla
   decía que faltaba la abstracción de correo, que sí existe desde el 2026-08-24.

### Cifras que la propia bitácora tenía mal

Al reverificar el 2026-08-24 se corrigieron dos números que se habían escrito de memoria:

- Las pruebas de CORS son **22 casos** (9 métodos con sus `InlineData`), no 12.
- El total de `Maquinaria.Api.Tests` era **116** ese día, y se confirmó corriendo la suite,
  no contando atributos a ojo. Un `[Theory]` con seis `InlineData` son seis pruebas para el
  corredor y una sola para quien lee el archivo, y ahí es donde se cuela el error.

**Y ese 116 quedó viejo al día siguiente.** El 2026-08-25 la suite está en **205**, medido
con `dotnet test`. Es la corrección más fácil de olvidar de todas, porque el número no se
rompe: simplemente deja de ser cierto y sigue leyéndose igual de bien. El reparto por
archivo, que suma exactamente 205:

| Archivo | Casos |
|---|---|
| `RestablecimientoPruebas.cs` | 29 |
| `FabricaConexionesEmpresaPruebas.cs` | 22 |
| `OrigenesPermitidosPruebas.cs` | 22 |
| `FormatoSlugPruebas.cs` | 21 |
| `RefrescoPruebas.cs` | 19 |
| `FormatoCodigoPlanPruebas.cs` | 18 |
| `ReintentoAltaPruebas.cs` | 18 |
| `EstadoEsquemaPruebas.cs` | 15 |
| `CrearPlanPruebas.cs` | 14 |
| `ContextoTenantPruebas.cs` | 11 |
| `HashContrasenasPruebas.cs` | 10 |
| `CatalogoPlanesTraduccionPruebas.cs` | 5 |
| `UnitTest1.cs` | 1 |

Los 89 casos nuevos respecto al 24 salen de dos bloques del mismo 2026-08-25: **37** del
catálogo de planes —`FormatoCodigoPlanPruebas` 18, `CrearPlanPruebas` 14,
`CatalogoPlanesTraduccionPruebas` 5— y **52** de los tres bloques de abajo:
`RefrescoPruebas` 19, `ReintentoAltaPruebas` 18 y `EstadoEsquemaPruebas` 15.
`Maquinaria.Dominio.Tests` sigue con la prueba de plantilla que genera `dotnet new`: 1 caso y
nada propio todavía.

## El catálogo de planes, y por qué no se puede editar — 2026-08-25

Los planes se crean y se retiran desde el panel; **no se editan**. No es una omisión, son dos
huecos del modelo que hay que cerrar antes de que editar sea seguro.

### 1. El precio no tiene historia

`suscripcion` guarda `tenant_id`, `plan_id`, `inicio`, `fin` y `estado` — **ningún importe**.
Así que cambiar `plan.precio_mensual` no solo cambia lo que pagan los suscriptores actuales:
reescribe lo que pagaron los históricos, porque no hay dónde estuviera guardado lo anterior.

Las dos salidas, y hay que elegir antes de facturar:

- **Congelar el precio en la suscripción**: agregarle `precio_mensual` y `moneda`, copiados
  del plan al contratar. Es lo mínimo y lo que hace casi todo el mundo.
- **Versionar el plan**: un plan nuevo por cada cambio de precio, con el anterior retirado.
  Más limpio conceptualmente y más ruidoso en el catálogo.

### 2. Editar los módulos cambia el acceso de quien ya lo tiene

El plan **es** su conjunto de módulos, así que quitarle uno se lo quita a todos sus
suscriptores, retroactivamente y sin aviso. El dominio ya lo advierte en `PlanModulo`: quien
necesite un módulo extra necesita otro plan.

Si esto se vuelve común, la salida que el propio dominio propone es un `tenant_modulo` de
excepción, espejo de `tenant_limite`: el plan sigue definiendo la base y el tenant declara
sus añadidos.

### Lo que sí es seguro, y por eso existe

`PATCH /planes/{codigo}/activo` retira o reactiva. Retirar **no toca a quien ya lo tiene
contratado** —su suscripción sigue apuntando al mismo plan con los mismos módulos—; lo único
que cambia es que el alta de empresas deja de aceptarlo, porque `AprovisionarEmpresa` ya
exigía que el plan estuviera activo.

### Pendiente en el frontend

El alta de empresa manda `codigoPlan: 'base'` **fijo en el código**
(`paginas/plataforma/empresas/empresas.ts`). Con el catálogo real, eso pasa a ser un selector
alimentado por `GET /planes` filtrando por activos.

## El comando `migrar-empresas` y la salud de esquemas — 2026-08-25

El paso 10 del plan de arranque, y el que llevaba más tiempo siendo el más urgente: las
migraciones de `ContextoEmpresa` se aplican **N veces, una por base**, y hasta hoy no había
nada que las aplicara en bloque ni nada que dijera quién se había quedado atrás.

Cómo se corre está en [puesta en marcha](../00-puesta-en-marcha.md#9-el-comando-migrar-empresas);
aquí van las decisiones.

**Es un argumento de `Maquinaria.Api`, no un proyecto de consola nuevo.** El comando necesita
exactamente la misma configuración que la API —las dos cadenas de conexión, que viven en los
*user secrets* de ese proyecto— y el mismo contenedor de DI. Un proyecto aparte serían otro
`.csproj`, otro juego de secretos y dos registros de infraestructura que pueden divergir, a
cambio de nada. Se ejecuta en `Program.cs` **antes** de configurar el pipeline: corre,
imprime y termina sin abrir ningún puerto.

**Va por la cadena directa**, `ConnectionStrings:Migraciones`, vía
`ProveedorContextoEmpresa.ParaMigrar`. Es la misma razón de siempre: el endpoint *pooled*
corre PgBouncer en modo transacción y por ahí no pasa DDL. Que el comando use el mismo camino
de código que el aprovisionamiento no es casualidad, es lo que evita tener dos formas de
llegar a la base de una empresa.

**Resistente a fallos parciales, que es su razón de existir.** Que la empresa 23 truene no
detiene a las que siguen, y no puede haber transacción que abarque varias bases porque son
bases distintas. Lo que vuelve manejable el fallo parcial es que cada base lleva su propia
`__EFMigrationsHistory` y que el historial es *append-only*: la que quedó atrás alcanza en la
siguiente corrida.

Los códigos de salida son la interfaz con un script de despliegue, así que son tres y no dos:

| código | significa |
|---|---|
| `0` | todas al día |
| `1` | al menos una falló — **las demás sí se migraron** |
| `2` | no se pudo ni empezar (típicamente, la central no responde) |

El reporte imprime una línea por empresa con `slug · estado · versiónAntes -> versiónDespués`,
y cuando hubo fallos **repite los slugs al final** en una línea `QUEDARON ATRAS: …`. Eso no
es adorno: con veinte empresas la línea del fallo se sale de la pantalla, y un reporte que
esconde el fallo entre el ruido es un reporte que nadie lee.

**La versión «antes» se lee de la base, no de la central.** Sale de la
`__EFMigrationsHistory` de cada empresa. La base es la verdad; `tenant.version_esquema` es
una copia, y si alguien aplicó una migración a mano esa copia está mal. Leyendo de la base, el
comando además **corrige** la central en lugar de heredar su error.

**No toca `estado_aprovisionamiento`.** Una empresa en `Fallida` sigue en `Fallida` después de
migrarla. Es deliberado: `Fallida` significa «el alta no terminó» y migrar no es dar de alta,
así que pisarlo esconderría un problema detrás del arreglo de otro. Las que no tienen base
salen `OMITIDA` —con el motivo, que es *reintenta el aprovisionamiento, no la migración*— y
**no cuentan** para el código de salida: si contaran, un tenant roto haría fallar el comando
para siempre y el `0` dejaría de significar nada.

### `GET /api/plataforma/salud/esquemas`

Policy de plataforma, `WithName("SaludDeEsquemas")`. Devuelve `versionDisponible` una sola vez
—es la misma para todas, porque es la del binario que responde—, `totalEmpresas`,
`desfasadas`, y por empresa `versionAplicada`, `migracionesPendientes`, `desfasada` y
`versionReconocida`.

**No lleva `nombre_bd`**, igual que `ResumenEmpresa`: el panel no necesita el nombre de la
base de un cliente para nada. El tipo interno `EmpresaConEsquema` **sí** lo lleva, porque el
comando necesita a qué base conectarse, y precisamente por eso ese tipo **nunca sale por
HTTP** — el caso de uso lo proyecta a `EstadoEsquemaEmpresa` antes de devolverlo. La
proyección no es ceremonia; es lo que deja el nombre de la base dentro del servidor.

### La decisión que merece explicarse: tres estados, no dos

`ComparadorEsquema.Comparar` es **pura** —no toca ninguna base ni construye ningún contexto— y
por eso es la única lógica no trivial de todo el bloque que se prueba sin Neon: 15 casos en
`EstadoEsquemaPruebas`.

Y reporta **tres** situaciones, no dos:

| resultado | qué significa |
|---|---|
| al día | la versión aplicada es la última del código |
| desfasada, con `migracionesPendientes` | falta aplicar N, y se sabe cuántas |
| `versionReconocida: false` | **no se pudo comparar** |

El tercero es el que importa. `versionReconocida: false` ocurre con `version_esquema` nula
—el alta no llegó a migrar, o la base se creó por fuera— y también cuando la versión aplicada
es una migración que **este binario no conoce**, es decir una base **por delante** del código
desplegado: un despliegue revertido, o una empresa migrada desde otra rama. Ahí no se inventa
un número de pendientes, porque no hay ninguno honesto que dar.

Colapsar esto a un solo booleano `desfasada` es lo que uno escribe sin pensarlo, y esconde el
caso peligroso detrás de un tranquilizador «está al día». Una base por delante del código es
exactamente la situación en la que la API va a fallar con errores de columna inexistente, y es
la que menos puede pasar desapercibida.

### La limitación consciente, marcada en el código

`SaludEsquemas` **lee `version_esquema` de la central y no se conecta a las bases de las
empresas**. Está marcado `ponytail:` en el propio archivo.

Consultar la `__EFMigrationsHistory` de N bases dentro de una petición HTTP son N conexiones y
N puntos de falla, y el dato ya lo mantienen los **dos únicos** caminos que aplican
migraciones: el aprovisionamiento y este comando. Consecuencia aceptada y escrita: si alguien
migra a mano sin actualizar la central, **el reporte miente** hasta la siguiente corrida de
`migrar-empresas`, que lo corrige. La simplificación se sostiene solo mientras esos dos
caminos sigan siendo los únicos que escriben ese campo; el día que aparezca un tercero, esto
hay que revisarlo.

`ListarConEsquemaAsync` **excluye las empresas con baja lógica**, al contrario que
`ListarAsync`: no hay que migrar la base de una empresa que ya no opera, y como el historial
es *append-only*, si algún día vuelve, alcanza.

### Fuera de alcance, dicho para que nadie lo busque

No hay endpoint HTTP que dispare la migración —se corre desde la terminal—, es secuencial sin
paralelismo, no hay `--solo <slug>` ni *dry-run*, y los logs de EF Core salen crudos a la
consola. Todo eso es cómodo y ninguno hace falta con dos empresas; el paralelismo además
querría pensarse dos veces contra un Postgres gestionado.

### Cómo se probó sin tocar Neon

Con las dos cadenas apuntando a `127.0.0.1:1`: el binario **reconoció el argumento, no abrió
ningún puerto y salió con `2`**, que es exactamente el camino de «no se pudo ni empezar». Eso
verifica el cableado —argumento, ámbito de DI, códigos de salida— sin aplicar **ninguna
migración a ninguna base real**. La corrida de verdad la hace el operador en su terminal, y
mientras no la haga, [el desfase sigue ahí](#el-desfase-de-esquema-dejó-de-ser-teórico).

**Trampa de operación heredada:** si hay un proceso de `Maquinaria.Api` vivo, el build se
bloquea con la DLL tomada. O se mata el proceso, o se compila una vez y se corre con
`--no-build`. Es la [trampa ya documentada](#trampa-de-operación-dos-instancias-de-la-api-a-la-vez),
y con este comando se pisa más seguido, porque uno lo lanza sin cerrar la API que tenía
levantada.

---

## Refresco rotativo y reintento del alta — 2026-08-25

Las dos piezas pequeñas que quedaban sobre mecánica ya construida. Salieron juntas, y una
destapó a la otra: el reintento es lo que encontró el agujero de la sección siguiente.

### `POST /api/empresas/{slug}/sesion/refresco`

**Anónimo, y tiene que serlo:** se refresca precisamente porque el token de acceso ya caducó,
así que exigir uno válido haría el endpoint inútil. Lo que autentica aquí es el token de
refresco. Va en un **grupo aparte** del `/api/mi` por dos razones concretas: el slug tiene que
ir en la ruta para que `MiddlewareTenant` resuelva la empresa —sin claim de tenant, resuelve
por ruta, y eso es lo que garantiza que la sesión se busque en la base de **esa** empresa y no
en otra— y para que el limitador pueda particionar. Reusa `EndpointsEmpresa.PoliticaAcceso`,
10 por minuto por slug e IP: el token es un secreto de 256 bits que no se adivina, pero el
endpoint es anónimo y escribe en la base.

**La respuesta es idéntica en forma a la del login** —el mismo `SesionEmpresa`—, para que el
cliente tenga un solo contrato de sesión y su interceptor pueda sustituir lo que tenía
guardado sin traducir nada. Aprender un segundo contrato para lo mismo es como se acumulan los
errores de sesión.

**Un solo 401 para seis motivos**, sin decir cuál: token inexistente, caducado, revocado,
reusado, usuario que ya no está activo, o empresa que no puede operar. Y un solo **tiempo**,
con piso uniforme entre rechazos, por la misma razón que en el login y en el
restablecimiento: distinguirlos le diría a quien prueba tokens y slugs cuáles existen.

**Detección de reuso, y el orden de las comprobaciones es la decisión.** Un token con
`reemplazado_por_id` no nulo dispara `RevocarSesionesDeAsync(usuarioId)` —**toda** la cadena
del usuario, no solo esa sesión—. Un token ya canjeado solo puede llegar de dos sitios: una
copia robada, o un cliente que perdió la respuesta de la rotación anterior. No se pueden
distinguir, así que se trata como robo: el costo del falso positivo es un login, el del falso
negativo es un atacante con acceso indefinido.

Se comprueba **antes** de `RevocadoEn` porque una sesión rotada tiene **las dos** marcas
puestas, y de las dos esta es la que significa «alguien está usando una copia». Si el orden se
invirtiera, todo reuso se leería como un simple token revocado y la cadena no se cerraría
nunca — el fallo silencioso perfecto.

**Un token caducado NO dispara la cadena**, y hay una prueba que lo fija. Caducar no es señal
de robo: es lo que le pasa a cualquiera que deja el navegador abierto un mes. Cerrarle todas
las sesiones por eso convertiría la defensa en una molestia diaria, y las defensas molestas se
acaban apagando.

**Los permisos se vuelven a resolver, no se copian del token viejo.** Es lo que hace que
revocar un permiso, cambiar un rol o retirar un módulo del plan surta efecto en 15 minutos —lo
que dura el token de acceso— y no en 30 días. Y el **estado del usuario se comprueba aquí**:
suspender a alguien tiene que cortarle el acceso sin esperar a que caduque su cadena.

**`RefrescarAsync` vive en `IniciarSesionEmpresa.cs` y no en una clase nueva.** No hace falta
ni una línea de DI nueva, y sobre todo: login y refresco comparten **cinco** cosas que no
deben divergir —la resolución del tenant, la compuerta `permisos del rol ∩ módulos del plan`,
la emisión del JWT, la vigencia del refresco y la forma de la respuesta—. Se extrajeron
`ResolverCompuertaAsync`, `NuevaSesion` y `Emitir` para que haya **una sola copia** de la
compuerta; el día que se ajuste una y se quede la otra atrás, «atrás» significa entregar
permisos sobre módulos que la empresa no contrató. De paso, un `AddDays(30)` literal pasó a la
constante `DiasVigenciaRefresco`.

### La trampa que hereda el cliente: la rotación no tiene ventana de gracia

Hay que dejarla escrita porque no es un defecto que se vaya a arreglar: es una propiedad del
diseño con la que el cliente tiene que vivir.

**Dos peticiones concurrentes con el mismo token cierran toda la sesión.** La segunda llega
cuando la primera ya canjeó el token, se lee como reuso, y la detección hace exactamente lo
que debe: revocar la cadena completa. Dos pestañas que despiertan a la vez, o un reintento
automático sobre un *timeout*, bastan.

Así que **el cliente está obligado a serializar sus refrescos**: un solo vuelo en curso y los
demás esperando su resultado (*single-flight*). Ya está resuelto en el frontend. La
alternativa —una ventana de gracia de unos segundos durante la cual el token viejo sigue
sirviendo— compraría tolerancia a costa de volver ambigua la señal de reuso, que es la única
defensa real contra un token robado. Se prefirió la señal nítida y la obligación en el
cliente.

### `POST /api/plataforma/empresas/{slug}/reintento`

Bearer de plataforma. Vuelve a correr los pasos **2 a 6** del aprovisionamiento, que son los
idempotentes: `ExisteBaseAsync` antes del `CREATE`, `Migrate()` que ya lo es de por sí, y un
sembrador que reusa el usuario y no deja dos invitaciones vigentes.

**Solo desde `Fallida`, y eso no es cortesía.** Reintentar sobre una empresa `Lista`
reemitiría la invitación de su administrador, y quien tuviera acceso al panel podría tomar esa
cuenta sin conocer su contraseña. Sobre una en `Creando` se solaparía con el intento que
todavía corre.

**La secuencia se extrajo a `EjecutarSecuenciaAsync`, compartida con el alta.** Si el
reintento tuviera su propia copia, cualquier arreglo de la secuencia habría que hacerlo dos
veces — y la segunda es la que se olvida.

**Solo pide correo y nombre del administrador.** El resto ya está en la fila del tenant. El
administrador se pide porque la central **no guarda a quién se invitó**: eso vive en la base
de la empresa. Ojo con lo que se deriva de esto, que es el tema de la sección de abajo.

**Revalida el formato del slug y que `nombre_bd` sea exactamente el derivado de él**, antes de
que llegue a concatenarse en un `CREATE DATABASE`. Es la restricción 2 del aprovisionamiento:
los identificadores SQL no se parametrizan. Que el valor venga de nuestra propia base central
no exime de comprobarlo, porque **este es el único camino del sistema que parte de un
`nombre_bd` ya almacenado en lugar de derivarlo de un slug recién validado**. Si no coincide,
se rechaza y se registra: un registro inconsistente se revisa a mano, no se aprovisiona.

**Los tres rechazos salen como 400**, no como 404 y 409 por separado, y está marcado
`ponytail:` en el endpoint. Es un endpoint del panel, ya autenticado, y lo único que la
interfaz hace con la respuesta es mostrar el texto del detalle; distinguir códigos no
cambiaría una línea del frontend. Devuelve **200 y no 201**: el tenant ya existía antes de la
llamada, así que no se creó ningún recurso nuevo.

**Pruebas nuevas:** `RefrescoPruebas.cs` (19 casos) y `ReintentoAltaPruebas.cs` (18).

---

## El agujero del sembrador de administradores — 2026-08-25

Lo más importante de la jornada, y no era una funcionalidad: era un fallo latente que llevaba
tiempo en el código y que **solo se volvió alcanzable al escribir el endpoint de reintento**.
Vale registrarlo completo, porque la lección no es sobre este error en particular.

### Qué pasaba

`SembradorAdministradorEf` recibía un correo y sembraba con él. Al reintentar un alta en
`Fallida` **con un correo distinto al del administrador ya sembrado**, creaba una **segunda
cuenta con acceso total** en la base de ese cliente, y mandaba la liga de invitación al correo
que venía en la petición.

Traducido: quien tuviera acceso al panel de superadministración podía **fabricarse una cuenta
con acceso total dentro de la base de un cliente y definirle la contraseña**. Y lo peor de
todo:

> Esa cuenta **no aparece en la interfaz de asignaciones**, porque el rol `administrador` no
> se asigna desde ahí — se otorga únicamente al aprovisionar.

O sea que ni el propio administrador de la empresa tenía dónde verla. Una cuenta con acceso
total, invisible en la única pantalla donde a alguien se le ocurriría buscarla.

### El arreglo

En `SembradorAdministradorEf.cs:25` se busca **primero** al usuario que ya tiene un rol con
`acceso_total`. **Si existe, gana ese:** el correo recibido se ignora y lo que hace el
reintento es reemitirle **su** invitación. Queda un `LogWarning` cuando los correos difieren,
así que un intento —o un dedazo— deja rastro en lugar de pasar en silencio.

Con eso la empresa mantiene **exactamente una persona con acceso total**, que es la garantía
que sostiene todo este flujo y la que el esquema ya defendía por su lado con
`UNIQUE INDEX rol_acceso_total_unico` y el trigger `rol_sistema_inmutable`. Ahí está el punto
fino: la base impedía un segundo **rol** con acceso total, y nada impedía un segundo
**usuario** con ese mismo rol. El motor cubría la mitad del invariante y la aplicación no
cubría la otra.

**Por eso `CrearAdministradorAsync` devuelve ahora `AdministradorSembrado(Correo, TokenEnClaro)`
y no el token suelto.** No es un refactor cosmético: **el correo al que se manda la liga tiene
que salir de ahí y no de la petición**. Mientras el llamador siguiera usando el correo de
entrada, el arreglo del sembrador no serviría de nada — la cuenta correcta con la liga a la
dirección equivocada es el mismo agujero con un paso más. El tipo de retorno es lo que impide
reintroducirlo por descuido: ya no queda una variable con el correo de la petición a mano en el
punto donde se arma el envío.

### La lección, que es la parte reutilizable

**Un endpoint nuevo sobre código idempotente no hereda solo su idempotencia: hereda también
sus supuestos.** El sembrador se escribió asumiendo que el correo que recibía era el del alta
—y en el alta lo es, porque el tenant se acaba de crear—. El reintento rompió ese supuesto sin
tocar ni una línea del sembrador.

Encaja con dos lecciones que ya estaban escritas en esta bitácora: *la base es la última línea
de defensa, no la primera* —aquí ni eso, porque el `UNIQUE` protegía el rol y no el usuario— y
*un mecanismo de protección hay que probarlo con todas las entradas que puede recibir, no solo
con las obvias*. Es la tercera vez que el mismo tipo de error aparece con otra cara.

### Lo que sigue abierto de esto

La operación de **recuperación de acceso total** —nombrar a otro administrador cuando esa única
persona se va— sigue **sin implementar**, como ya decía la sección de control de usuarios y
permisos. Y ahora se sabe algo más sobre cómo tiene que ser: no puede ser un efecto colateral
del reintento del alta, tiene que ser una operación explícita, con su propio nombre y su propia
autorización, auditada con `origen = 'plataforma'`. Que es justo lo que **no se podrá auditar
hasta que el interceptor exista** — otra razón para que sea lo siguiente.
