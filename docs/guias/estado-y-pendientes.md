# Estado y pendientes

Última verificación: 2026-08-21.

## Estado actual

**El esquema de la Fase 0 está completo y el primer login funciona.** Las 9 tablas de plataforma en `maquinaria_central` y las 10 de empresa en `maquinaria_plantilla` (Neon, rama `dev`), con sus `CHECK`, sus índices y el constraint `EXCLUDE` de no-traslape verificados contra la base real. `dotnet build --no-incremental` en verde con 0 advertencias y sin paquetes vulnerables.

**`/openapi/v1.json` ya no está vacío:** expone `POST /api/plataforma/sesion` y `GET /api/plataforma/sesion/actual`. Un superadministrador inicia sesión, recibe un JWT y accede a un endpoint protegido, comprobado de punta a punta contra Neon.

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
- [ ] El interceptor de auditoría: **bloqueado por la auth**, necesita `usuario_id`, `roles`, `ip` y `origen` del contexto de la petición
- [x] **Servicio de aprovisionamiento**, con su endpoint `POST /api/plataforma/empresas`. Probado creando una empresa real de punta a punta
- [x] Abstracción de correo `IEnviadorCorreo`, con `CorreoEnLog` para desarrollo y `CorreoResend` para la nube
- [x] `GET /api/plataforma/empresas`: listado con estado de aprovisionamiento, plan y módulos. Usa subconsultas y no joins, para que un tenant **sin** suscripción aparezca con plan nulo en lugar de desaparecer — que son justo los que hay que ver
- [ ] Comando `migrar-empresas` + endpoint de salud que reporte quién quedó atrasado
- [ ] Endpoint para **reintentar** un alta en `Fallida`. La secuencia ya es idempotente; falta el disparador
- [x] **Resolución de conexión por empresa**: `IDirectorioTenants` con caché, `IContextoTenant` de ámbito de petición, `MiddlewareTenant`, `FabricaConexionesEmpresa` y `ProveedorContextoEmpresa`. Ver [`01-arquitectura.md`](../01-arquitectura.md) §2.0
- [ ] Auth de **empresa**: login por empresa/correo/contraseña, refresh rotativo, invitaciones
- [x] Manejo global de errores (`IExceptionHandler` → ProblemDetails, sin filtrar mensajes de excepción al cliente) y health check `/salud` de la base central
- [x] Auth de **plataforma**: PBKDF2, JWT con audiencia propia, policy de ámbito, limitador de intentos por IP, y siembra del primer superadministrador desde secretos
- [ ] Logging estructurado con enriquecimiento por petición (falta el `correlacion_id` que compartirá con la auditoría)
- [ ] Abstracción de almacenamiento de archivos con implementación en disco
- [ ] Convenciones de equipo: ramas, commits, revisión, acceso a Neon (los remotos de GitHub ya están: `xYairrx/maquinaria-backend` y `xYairrx/maquinaria-frontend`, rama `develop`)

### Criterio de salida de Fase 0

Un superadministrador da de alta una empresa desde el panel, el sistema le crea y migra su base automáticamente, se envía la invitación al primer administrador, esa persona define su contraseña e inicia sesión con `empresa / correo / contraseña`. Y el comando `migrar-empresas` aplica una migración nueva a todas las bases existentes reportando el resultado por empresa.

### Orden de trabajo

Cerrados los pasos **7**, **8** y la mitad de plataforma del **12**. El orden que sigue, ya reordenado:

1. ~~Resolución de conexión por empresa.~~ **Hecha.**
2. ~~Aprovisionamiento.~~ **Hecho**, salvo el endpoint de reintento.
3. **Comando `migrar-empresas`** (el 10) y el endpoint de salud que reporta quién quedó atrasado.
4. **Auth de empresa** (el 12): invitación, definición de contraseña y login con slug.
5. **Interceptor de auditoría**, desbloqueado en cuanto haya contexto de petición autenticada.

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

**Lo que falta de esta pieza:** el endpoint que dispara el reintento. La secuencia es
idempotente y el registro queda en `Fallida`, pero nada lo vuelve a llamar todavía. Y
`CorreoResend` **no se ha ejercitado contra la API real**: hace falta la llave y un dominio
verificado.

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

Es exactamente el escenario para el que existe `migrar-empresas`, y el comando **no está
escrito**. Por ahora hay que aplicar a mano, base por base. Con dos empresas se aguanta;
con veinte, no. Sube de prioridad.

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
