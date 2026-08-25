# Configuración

Cómo conectar el backend a la base de datos y dónde vive cada secreto.

## Por qué hay dos cadenas de conexión

El sistema usa **una base de datos por empresa**, todas en el mismo proyecto de Neon:

```mermaid
graph LR
    API[Maquinaria.Api]
    API -->|cadena fija| C[(Base central<br/>tenant, tenant_limite, plan<br/>modulo, plan_modulo, tipo_limite<br/>suscripcion, usuario, auditoria)]
    API -->|cadena resuelta<br/>por peticion| E1[(maquinaria_bajio<br/>usuario, equipos<br/>clientes, rentas...)]
    API --> E2[(maquinaria_norte<br/>usuario, equipos<br/>clientes, rentas...)]
    API --> E3[(maquinaria_...)]
```

`ContextoCentral` usa una cadena fija de configuración. `ContextoEmpresa` se construye por petición clonando la central y cambiándole el nombre de la base por `tenant.nombre_bd`. Un solo endpoint, N bases.

Pero dar de alta una empresa implica `CREATE DATABASE` y correr migraciones, y eso **no funciona por el endpoint pooled**. De ahí las dos cadenas.

## Las dos cadenas

| Endpoint | Clave | Uso |
|---|---|---|
| **Con** `-pooler` en el host | `ConnectionStrings:Central` | Runtime de la API |
| **Sin** `-pooler` (directo) | `ConnectionStrings:Migraciones` | Migraciones y el `CREATE DATABASE` del aprovisionamiento |

El endpoint pooled corre **PgBouncer en modo transacción**: la conexión física vuelve al pool al terminar cada transacción, lo que descarta todo estado de sesión (`SET`, tablas temporales, `LISTEN/NOTIFY`) y también el DDL.

> Si inviertes las dos cadenas el síntoma es tardío y confuso: la API funciona perfecto y el error aparece cuando corres la primera migración o das de alta la primera empresa.

## Formato de la cadena

**Npgsql no acepta el formato URI** (`postgresql://usuario:password@host/base`) que Neon muestra por defecto. Necesita el formato ADO.NET de `clave=valor`:

```
Host=ep-xxx.c-12.us-east-1.aws.neon.tech; Database=maquinaria_central; Username=neondb_owner; Password=...; SSL Mode=VerifyFull; Channel Binding=Require;
```

En el panel **Connect** de Neon: elige la rama `dev` (no `production`), la base `maquinaria_central` en el selector de *Database* —así la cadena ya viene con el `Database=` correcto— y el framework **.NET**. El toggle de *Connection pooling* es lo único que agrega o quita el sufijo `-pooler`; el segmento `c-12` se queda en las dos.

`SSL Mode=VerifyFull` es lo que da Neon hoy y es lo que se usa. Además de cifrar, valida la cadena del certificado y que el hostname coincida, lo que detecta un man-in-the-middle que `Require` no vería. Funciona con Neon sin configuración extra.

Tres trampas al copiar la cadena, las tres verificadas en carne propia:

- **El snippet viene entre comillas dobles.** Son para pegarlo en un `appsettings.json`. Si quedan dentro del valor del secreto, Npgsql recibe una cadena que empieza con `"` y falla el parseo. El valor debe empezar en `Host=`.
- **El password se muestra enmascarado.** Hay que darle a *Show password* y confirmar que lo copiado trae el `npg_...` real y no `****`. El error que produce habla de autenticación, no de asteriscos.
- **La pestaña `Entity Framework (appsettings.json)` no se usa en este proyecto.** Te da un fragmento listo para pegar en un archivo que se commitea. Quédate en *Connection string*.

### En qué terminal

En **PowerShell**, con **comillas simples** alrededor del valor: las dobles interpolan `$`, y un `$` en el password te guarda la cadena mutilada.

```bash
dotnet user-secrets set 'ConnectionStrings:Central' 'Host=...;' --project src/Maquinaria.Api
```

En **cmd** esto no funciona: la comilla simple no delimita nada, y si copias un marcador de posición con `<` o `>` cmd los interpreta como **redirección** y responde *El sistema no puede encontrar el archivo especificado* — un mensaje que no menciona a `dotnet` ni a los secretos.

Para verificar sin volcar los valores:

```bash
dotnet user-secrets list --project src/Maquinaria.Api | ForEach-Object { ($_ -split '=')[0].Trim() }
```

## Dónde van los secretos

En desarrollo, **user secrets**. En Railway, **variables de entorno**. Nunca en `appsettings.json`.

`appsettings.json` se commitea, y el `.gitignore` que genera .NET **no lo excluye** — tampoco `appsettings.Development.json`. Una cadena ahí entra al historial de git, y sacarla después obliga a reescribir historial *y* rotar la credencial de todos modos, porque ya quedó en reflogs, clones y en el escaneo de secretos de GitHub.

En este proyecto pesa más de lo normal: todas las bases de empresa comparten proyecto y endpoint de Neon, así que esa credencial da acceso a **todos** los tenants, presentes y futuros. Una fuga no es el incidente de un cliente, es el sistema completo.

```bash
dotnet user-secrets set "ConnectionStrings:Central" "<cadena con -pooler>" --project src/Maquinaria.Api
```

```bash
dotnet user-secrets set "ConnectionStrings:Migraciones" "<cadena sin -pooler>" --project src/Maquinaria.Api
```

Los secretos viven en `%APPDATA%\Microsoft\UserSecrets\`, fuera del repo. Para confirmar que quedaron **sin volcar las contraseñas** a la terminal:

```bash
dotnet user-secrets list --project src/Maquinaria.Api | ForEach-Object { ($_ -split '=')[0].Trim() }
```

Son por usuario y por máquina, y están en texto plano: protegen contra fugas por git, no contra alguien con tu sesión de Windows. Y **no viajan con el repositorio** — ver [los cuatro secretos del arranque](#los-cuatro-secretos-que-exige-el-arranque).

Las dos cadenas no son los únicos secretos. La lista completa está abajo, y son cuatro.

## Los cuatro secretos que exige el arranque

Verificado contra el código el 2026-08-24. Sin ellos el repo compila y no sirve, y cada uno falla en un momento distinto — que es justo lo que vuelve confuso el diagnóstico:

| Clave | Qué es | Dónde revienta si falta |
|---|---|---|
| `ConnectionStrings:Central` | La cadena **con** `-pooler` | **Al arrancar.** `SembrarSuperadminAsync` corre antes de `app.Run()` y consulta la base; `RegistroInfraestructura` lanza `Falta ConnectionStrings:Central` |
| `ConnectionStrings:Migraciones` | La cadena **directa, sin** `-pooler` | En `dotnet ef` y en el alta de una empresa, no al arrancar. `FabricaConexionesEmpresa` la necesita en **tiempo de ejecución** para el `CREATE DATABASE` |
| `Jwt:Llave` | Mínimo **32 bytes**, para HMAC-SHA256 | En el **primer login**. `ProveedorTokensJwt` valida el largo en su constructor, y es singleton: se construye al resolverlo, no al arrancar |
| `Arranque:Superadmin:{Correo,Contrasena,Nombre}` | El primer superadministrador | **No revienta: se salta en silencio.** El sembrador registra *«Sin Arranque:Superadmin configurado»* y sigue. El resultado es una API viva contra la que nadie puede iniciar sesión, porque no hay registro público en ninguna parte |

Dos detalles que cuestan un rato:

- **`Contrasena` va sin eñe.** El sembrador lee `seccion["Contrasena"]`; `Contraseña` con eñe no lo encuentra y cae en el caso de "no configurado", que no es un error sino un log informativo que pasa desapercibido.
- **`Nombre` es opcional**, con `"Superadministrador"` por defecto. Los que de verdad hacen falta son `Correo` y `Contrasena`: si falta cualquiera de los dos, no se siembra nada.

```bash
dotnet user-secrets set "Jwt:Llave" "<32 bytes o más>" --project src/Maquinaria.Api
dotnet user-secrets set "Arranque:Superadmin:Correo" "<correo>" --project src/Maquinaria.Api
dotnet user-secrets set "Arranque:Superadmin:Contrasena" "<contraseña>" --project src/Maquinaria.Api
```

### Los user secrets no viajan con el repositorio

Viven en `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`, y **OneDrive no sincroniza esa carpeta**. El código sí está en OneDrive; los secretos no.

La consecuencia práctica: clonar el repo en otra máquina —o restaurar esta— deja un backend que compila y no arranca, con un error de conexión que no menciona en ninguna parte que el problema sea un archivo ausente. Es reconfiguración manual, no un fallo. El `UserSecretsId` del proyecto es `e2ab6bfa-6850-4b07-ba0b-06a9e777cd27`.

## Resend

`Correo:Proveedor` pasó a `"resend"` en `appsettings.json` el **2026-08-24**. Antes era `"log"`, que escribía el correo al log en lugar de enviarlo.

Eso convierte a `Resend:Llave` en un secreto más:

```bash
dotnet user-secrets set "Resend:Llave" "<re_...>" --project src/Maquinaria.Api
```

No revienta al arrancar: `CorreoResend` trata el envío como *best-effort* y registra el fallo sin propagarlo, porque que Resend esté caído no puede tumbar el alta de una empresa. El síntoma de una llave ausente es entonces un correo que nunca llega, con el motivo únicamente en el log.

**Mientras el dominio no esté verificado en Resend, la cuenta está en sandbox**, y eso significa dos restricciones que parecen errores de configuración y no lo son:

1. El único remitente que acepta es **`onboarding@resend.dev`** — que es el valor por defecto de `Correo:Remitente`.
2. Solo **entrega al correo del titular de la cuenta**. Una invitación a `admin@demo.mx` se acepta con 200 y no llega a ninguna parte.

Para probar el flujo completo de invitación o de restablecimiento con una dirección real hace falta verificar un dominio primero.

## Referencia de configuración

Lo que vive en `appsettings.json` y se commitea a propósito. Los valores de la columna *Desarrollo* salen de `appsettings.Development.json`, que gana sobre el base.

| Clave | Default | Desarrollo | Para qué |
|---|---|---|---|
| `Jwt:Emisor` | `maquinaria` | — | Se valida al recibir |
| `Jwt:AudienciaPlataforma` | `maquinaria-plataforma` | — | Tokens de superadministrador |
| `Jwt:AudienciaEmpresa` | `maquinaria-empresa` | — | Tokens de usuario de empresa |
| `Jwt:MinutosPlataforma` | `60` | — | Larga porque la plataforma no tiene refresco |
| `Correo:Proveedor` | `resend` | — | `log` o `resend` |
| `Correo:Remitente` | `onboarding@resend.dev` | — | Debe ser de un dominio verificado |
| `Correo:UrlBaseAplicacion` | `http://localhost:4200` | — | Base de las ligas. El slug se le inserta en el **host** |
| `Correo:DevolverLigaEnRespuesta` | `false` | `true` | En producción regalaría la sesión del administrador de un cliente |
| `Correo:DiasVigenciaInvitacion` | `7` | — | La vigencia del **restablecimiento no está aquí**: es una hora y vive en el dominio |
| `MultiTenancy:SegundosCacheTenant` | `60` | — | Caché del directorio de tenants |
| `MultiTenancy:PrefijoBaseDatos` | `maquinaria_` | — | `nombre_bd` = prefijo + slug |
| `Cors:Origenes` | — | `http://localhost:4200` | Orígenes **exactos**. Aquí van los de la plataforma, que no son subdominios de cliente |
| `Cors:DominioBase` | vacío | `localhost` | Dominio bajo el cual **cualquier** subdominio vale. Vacío desactiva la regla |
| `Cors:ExigirHttps` | `true` | `false` | Se apaga solo en desarrollo, donde el dev server es http |

### `Cors:DominioBase`, y por qué no es una lista

Cada empresa vive en su propio subdominio, así que el conjunto de orígenes válidos es **abierto**: crece con cada cliente. Una lista en configuración obligaría a redesplegar la API cada vez que se da de alta una empresa. La decisión está en `src/Maquinaria.Api/Arranque/OrigenesPermitidos.cs`, con el porqué de cada comprobación.

`DominioBase = localhost` habilita `bajio.localhost:4200` **sin tocar el archivo `hosts`**: Chrome y Edge resuelven `*.localhost` a `127.0.0.1` de forma nativa.

La comparación es `host == dominio || host.EndsWith("." + dominio)`, y el **punto del prefijo es lo que la hace segura**: `malo-ejemplo.com` termina en `-ejemplo.com`, no en `.ejemplo.com`. Sin ese punto, un `EndsWith("ejemplo.com")` regalaría el CORS a un dominio ajeno.

**Lo que esta comprobación no hace:** verificar que el subdominio sea una empresa real. Sería una consulta a la base en cada preflight y delataría qué slugs son clientes — justo lo que evitan las reglas anti-enumeración del login. Que el tenant exista lo resuelve la petición, no el CORS.

> **Pendiente menor:** el `appsettings.json` base trae `Cors:Origenes: ["*.localhost:4200"]`. La lista exacta se compara con `Contains`, así que un comodín ahí no coincide con nada y además no es un origen válido —le falta el esquema—. En desarrollo no se nota porque `appsettings.Development.json` sustituye la lista entera. Es configuración muerta que conviene limpiar.

## Precedencia

`IConfiguration` resuelve en este orden, de menor a mayor prioridad:

```
appsettings.json  →  appsettings.{Environment}.json  →  user secrets  →  variables de entorno
```

La misma clave se lee igual en los tres lados; solo cambia de dónde viene. En local gana el user secret, en Railway gana la variable de entorno. No hace falta código condicional ni `#if DEBUG`.

User secrets solo se cargan cuando el entorno es `Development`, lo cual es justo el diseño buscado.

## Qué sí va en appsettings.json

Todo lo que no es secreto, y se commitea a propósito porque es configuración documentada: niveles de logging, issuer/audience y vigencia del JWT, el dominio base del CORS, qué implementación de `IEnviadorCorreo` y de `IAlmacenamientoArchivos` usar, zona horaria por defecto. El inventario clave por clave está en la [referencia de configuración](#referencia-de-configuración).

## Neon

| Campo | Valor |
|---|---|
| Proyecto | `maquinaria` |
| Postgres | 18 |
| Región | AWS US East 1 (N. Virginia) — **irreversible** |
| Rama de desarrollo | `dev` (padre `production`, auto-delete en **Never**) |
| Neon Auth | desactivado |
| Extensiones | `btree_gist` 1.8, `pg_trgm` 1.6 disponibles; se crean por migración |

**La región no se puede cambiar después** y debe coincidir con la de Railway. Es el factor de rendimiento número uno del sistema: misma región son 1-2 ms, regiones distintas dentro de EE. UU. son 15-60 ms. Con ~20 consultas por pantalla, es la diferencia entre 40 ms y 1.2 s. Railway US East está en Virginia, **no** en Ohio.

La rama `dev` necesita auto-delete en `Never`: el default *After 1 day* la borraría al día siguiente.

Neon Auth va desactivado porque choca con el modelo de base por empresa y con la matriz rol × módulo × acción.

En plan gratuito Neon **suspende el cómputo** tras unos minutos de inactividad, así que la primera consulta después de una pausa tarda. Aceptable en desarrollo, no para una demo con cliente.

## Las bases de datos de desarrollo

Decidido el 2026-08-20 (ver [estado y pendientes](estado-y-pendientes.md#4-nombres-de-las-bases-de-datos)). En la rama `dev` del proyecto de Neon:

| Base | Contexto | Para qué |
|---|---|---|
| `maquinaria_central` | `ContextoCentral` | La central. Sustituye al `neondb` por defecto |
| `maquinaria_plantilla` | `ContextoEmpresa` | **Solo tiempo de diseño.** Blanco inofensivo para `dotnet ef database update` y sitio donde inspeccionar el esquema de empresa generado |
| `neondb` | — | La que crea Neon. No se usa |

Las bases de empresa siguen el patrón `maquinaria_<slug>` y las crea el aprovisionamiento, nunca a mano.

**Solo hay dos cadenas de conexión, no tres** (los otros dos secretos del arranque son `Jwt:Llave` y el superadministrador — ver [los cuatro secretos](#los-cuatro-secretos-que-exige-el-arranque)). La fábrica de tiempo de diseño de `ContextoEmpresa` no lleva cadena propia: toma `ConnectionStrings:Migraciones` y le sustituye el `Database=` por `maquinaria_plantilla` con `NpgsqlConnectionStringBuilder`. Así no hay un tercer valor que se desincronice, y es el mismo camino de código que el runtime usa para resolver la base de cada empresa.

**`plantilla` y `central` quedan reservados como slugs de tenant**, porque un slug `plantilla` produciría `nombre_bd = maquinaria_plantilla` y chocaría con esta base.

### Verificado el 2026-08-20

Ambas cadenas conectan contra Neon: PostgreSQL **18.6**, usuario `neondb_owner`, región `us-east-1`. `btree_gist` 1.8 y `pg_trgm` 1.6 aparecen **disponibles pero no instaladas** en ninguna base, que es lo correcto: se instalan por migración, no a mano.
