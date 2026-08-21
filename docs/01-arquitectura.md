# Arquitectura — Sistema Integral de Operación y Rentabilidad de Activos

> SaaS multi-tenant para empresas de renta de maquinaria y equipo.
> Documento vivo. Última actualización: 2026-08-19.

---

## 1. Stack

| Capa | Tecnología | Alojamiento |
|---|---|---|
| Backend | .NET 10 (LTS, soporte hasta nov 2028) | Railway (contenedor Docker) |
| ORM | EF Core 10 + Npgsql | Code-First, migraciones versionadas |
| Base de datos | PostgreSQL | Neon (gestionado) |
| Frontend | Angular 22 (standalone + signals) | Cloudflare Pages |
| Archivos | S3-compatible vía `IAlmacenamientoArchivos` | Cloudflare R2 |
| DNS / CDN / WAF | — | Cloudflare |
| Auth | JWT propio + refresh token rotativo | — |
| Repositorio | Dos repos: `maquinaria_back` (con `docs/`) y `maquinaria_front` | GitHub |
| Nomenclatura | Dominio en español | `Equipo`, `Renta`, `Cotizacion`, `Horometro` |

Versiones verificadas el 2026-08-17: Angular CLI 22.1.4, SDK .NET 10.0.302.

**Por qué .NET 10 y no 9:** ya es LTS, está instalado en la máquina, y EF Core 10 trae mejoras en `ExecuteUpdate`/`ExecuteDelete` y traducción de LINQ que este proyecto va a usar mucho en los reportes.

**Sobre librerías de mediator:** no usamos MediatR (cambió a licencia comercial en 2025). Los casos de uso son clases con un método `Ejecutar`, registradas en el contenedor de DI. Menos magia, menos dependencias, el mismo resultado.

---

## 2. Multi-tenancy: una base de datos por empresa

Modelo **multi-database**. Una base central con el catálogo de empresas, y **una base de datos independiente por cada empresa suscrita**, creada y migrada automáticamente al darla de alta.

```
Base central              tenant, tenant_limite, plan, modulo, plan_modulo,
                          tipo_limite, suscripcion, usuario, auditoria
Base maquinaria_bajio     usuario, equipos, clientes, rentas...
Base maquinaria_norte     usuario, equipos, clientes, rentas...
```

### 2.0 Cómo se resuelve la base de cada petición

Tres piezas, y una garantía.

```
JWT (claim tenant) → MiddlewareTenant → IDirectorioTenants → IContextoTenant
                                              │                     │
                                        caché + central       ContextoEmpresa
```

| pieza | qué hace |
|---|---|
| `IDirectorioTenants` | resuelve slug o id → `TenantResuelto`, con **caché**. Una resolución cuesta tres lecturas: el tenant, sus límites propios y los módulos del plan de su suscripción vigente |
| `MiddlewareTenant` | lee el claim `tenant` del JWT, resuelve, y verifica que la empresa **pueda operar** antes de dejar pasar |
| `IContextoTenant` | portador de ámbito de petición. `ContextoEmpresa` se registra con un `optionsAction` que lo consulta |
| `FabricaConexionesEmpresa` | el **único** lugar donde un `nombre_bd` se vuelve cadena de conexión, y por eso el único donde hay que validarlo |

**La garantía: no existe una base por defecto.** `IContextoTenant.Actual` **lanza** si no hay empresa resuelta. Si un camino de código llega a abrir un `ContextoEmpresa` sin tenant, revienta ruidosamente en lugar de caer a la central, a la plantilla o a la última usada — cualquiera de esas tres sería una fuga entre clientes esperando a que alguien la encuentre. Reasignar el tenant a media petición también lanza.

**Se verifica en cada petición, no solo en el login.** Suspender a un cliente tiene que surtir efecto sin esperar a que caduquen los tokens que ya se emitieron, así que `PuedeOperar` —estado comercial más aprovisionamiento `Lista`— se comprueba en el middleware. Lo que acota el desfase es el TTL de la caché: con varias instancias en Railway cada una tiene la suya, y la invalidación explícita solo alcanza a una.

**Los tenants inexistentes no se cachean.** Cachear ausencias sería un camino para llenar la memoria del servidor pidiendo slugs al azar.

**`nombre_bd` no sale del servidor.** No viaja en el JWT ni en ninguna respuesta: un JWT va firmado pero **no cifrado**, y los nombres de las bases de los clientes no son información para el navegador. El token lleva el id del tenant; el `nombre_bd` lo resuelve el servidor.

**La aplicación necesita las DOS cadenas de conexión en tiempo de ejecución.** La *pooled* atiende peticiones; la *directa* la necesita el aprovisionamiento, porque `CREATE DATABASE` es DDL y el endpoint pooled corre PgBouncer en modo transacción y no lo admite. Hasta ahora `ConnectionStrings:Migraciones` solo se usaba en tiempo de diseño; el despliegue en Railway tiene que llevar ambas.

### 2.1 Por qué este modelo y no una base compartida

La alternativa era una sola base con `tenant_id` en cada tabla y Row-Level Security. Se descartó por tres razones del negocio:

1. **El aislamiento es físico, no lógico.** No existe la posibilidad de que una consulta alcance datos de otra empresa: están en bases distintas. Es también lo más fácil de explicar y de vender.
2. **Se vende el software como copia permanente.** Entregar una instalación es llevarse su base. Con base compartida habría que extraer sus datos de en medio de los de todos.
3. **Respaldo y restauración por cliente son triviales.**

Y trae una simplificación grande de toda la capa de datos:

| Ya no hace falta | Por qué |
|---|---|
| `tenant_id` en ~70 tablas | Cada base tiene un solo cliente |
| Políticas de RLS y `FORCE ROW LEVEL SECURITY` | El aislamiento es físico |
| Interceptor de `SET LOCAL app.tenant_id` | — |
| Rol de base de datos separado para la aplicación | — |
| La prueba de fuga entre tenants | No hay fuga posible |
| `UNIQUE (tenant_id, correo)` | Basta `UNIQUE (correo)` |

### 2.2 El costo, que es operativo

**Las migraciones se corren N veces.** Cada despliegue aplica las migraciones de `ContextoEmpresa` en cada base. Si falla en la empresa 23, quedan versiones desalineadas. Se administra con dos mecanismos:

- `tenant.version_esquema` — qué migración tiene aplicada cada empresa.
- Un endpoint de salud que reporte quién quedó atrasado. Sin él, el desfase es invisible hasta que algo truena.

**Regla derivada, sin excepciones:** las migraciones **nunca se aplastan ni se reescriben** después de un release. Una empresa puede estar dos versiones atrás y tiene que poder alcanzar. El historial es *append-only* para siempre.

Este modelo escala bien a decenas de clientes y se vuelve pesado en los miles. Es el rango correcto para este producto.

### 2.3 Dos `DbContext`

| Contexto | Base | Cadena de conexión |
|---|---|---|
| `ContextoCentral` | La central | Fija, de configuración |
| `ContextoEmpresa` | La de cada empresa | **Resuelta en tiempo de ejecución** |

`ContextoEmpresa` se construye por petición con la cadena de la empresa: se clona la central y se le cambia el nombre de la base por `tenant.nombre_bd`. Como el esquema es idéntico en todas, EF Core cachea el modelo una sola vez.

### 2.4 Aprovisionamiento

Dar de alta una empresa es una secuencia, no un `INSERT`: crear el registro, `CREATE DATABASE`, migrar, sembrar permisos y roles, crear el primer administrador y enviarle su invitación.

Tres detalles críticos de PostgreSQL, desarrollados en `05-esquema-fase0.md` §5:

- `CREATE DATABASE` **no corre dentro de una transacción**, y EF Core envuelve en transacción por defecto.
- El nombre de la base **no se puede parametrizar**, así que la sentencia se concatena. La validación del formato del nombre es control de seguridad, no cosmética.
- Crear el registro y crear la base **no son atómicos**. Por eso `tenant.estado_aprovisionamiento` deja un registro reintentable en lugar de un huérfano.

---

## 3. Qué vive en cada base

| Base | Contenido | Tablas |
|---|---|---|
| **Central** | El negocio del SaaS | `tenant`, `tenant_limite`, `plan`, `modulo`, `plan_modulo`, `tipo_limite`, `suscripcion`, `usuario`, `auditoria` |
| **Empresa** | Todos los datos del cliente | Todo lo demás: usuarios, permisos, equipos, clientes, rentas… |

**Los catálogos generales** —marcas, modelos, categorías de maquinaria— se **siembran** en cada base de empresa durante el aprovisionamiento, con el mismo contenido base. Cada empresa puede agregar los suyos sin afectar a nadie. Así una empresa nueva ve un catálogo poblado desde el primer minuto, que era la ventaja buscada, y sin compartir tabla con nadie.

Consecuencia a tener presente: actualizar el catálogo maestro implica propagar la semilla a las bases existentes mediante una migración, no un `UPDATE` central.

**Los superadministradores** viven solo en la base central y no existen en ninguna base de empresa. Un error de permisos dentro de una empresa no puede alcanzar la plataforma.

---

## 4. Estructura de la solución

Dos repositorios independientes, en una carpeta contenedora que **no** es un repo:

```
Documents/Maquinaria/
├── maquinaria_back/                   # repo 1 → Railway
│   ├── docs/                          # este directorio
│   ├── Maquinaria.slnx                # formato .NET 10, no .sln
│   ├── Directory.Packages.props       # versiones de paquetes centralizadas
│   ├── src/
│   │   ├── Maquinaria.Dominio/        # entidades, enums, reglas puras. Sin dependencias.
│   │   ├── Maquinaria.Aplicacion/     # casos de uso por módulo, DTOs, validaciones, interfaces
│   │   ├── Maquinaria.Infraestructura/# EF Core, Npgsql, almacenamiento, JWT
│   │   └── Maquinaria.Api/            # endpoints minimal API, DI, middleware de tenant
│   └── tests/
│       ├── Maquinaria.Dominio.Tests/
│       └── Maquinaria.Api.Tests/      # integración contra Postgres real
└── maquinaria_front/                  # repo 2 → Cloudflare Pages
```

Salida del build del front, para configurar Cloudflare Pages: `dist/maquinaria-front/browser`.

Dentro de `Aplicacion` la organización es **por módulo, no por tipo técnico**:

```
Aplicacion/
├── Equipos/           ← CrearEquipo.cs, ObtenerExpediente.cs, EquipoDto.cs
├── Rentas/
├── Disponibilidad/
└── Mantenimiento/
```

Con 26 módulos, carpetas `Services/`, `Repositories/`, `Validators/` se vuelven inmanejables. Feature folders mantienen junto lo que cambia junto.

---

## 5. Convenciones de base de datos

Decisiones que se toman una vez y no se vuelven a discutir:

| Tema | Decisión | Razón |
|---|---|---|
| Llaves primarias | `uuid` v7 (`Guid.CreateVersion7()`) | Ordenable en el tiempo, sin colisiones entre tenants, no filtra volumen de negocio como un autoincremental |
| Nombres | `snake_case` en BD, `PascalCase` en C# | Convención de Postgres; se mapea con un traductor de nombres |
| Dinero | `numeric(18,4)` | **Nunca** `float`/`double`. 4 decimales para tarifas por hora |
| Fechas | `timestamptz`, siempre UTC | La zona horaria de presentación se guarda por tenant |
| Rangos de fecha | `tstzrange` | Habilita los constraints de no-traslape (ver `02-modelo-datos.md`) |
| Borrado | Lógico (`eliminado_en`) en entidades de negocio | Un equipo o una renta nunca se borran de verdad; hay auditoría e historial que dependen de ellos |
| Enums | `smallint` en BD + `enum : short` en C# | Más simple de migrar que los enums nativos de Postgres. Se declara `: short` para que EF deduzca `smallint` sin configurar cada propiedad. Los valores arrancan en 1, nunca en 0, y un `CHECK` acota el rango |
| Extensiones | `btree_gist`, `pg_trgm` | Constraints de exclusión y búsqueda por texto. **No** se necesita ninguna extensión de UUID: los generamos en C# con `Guid.CreateVersion7()` |

---

## 6. Seguridad y permisos

El documento pide 9 roles y permisos por módulo con 6 acciones (consulta, alta, edición, eliminación, autorización, exportación). Eso es una matriz, no un enum de roles.

```
Usuario ──< UsuarioRol >── Rol ──< RolPermiso >── Permiso
                                                     │
                                          "equipos.editar"
                                          "rentas.autorizar"
                                          "reportes.exportar"
```

Los permisos son cadenas `modulo.accion`, se resuelven al iniciar sesión y **viajan en el JWT**, con token de vida corta y refresh rotativo. Se descartó resolverlos por petición o cachearlos en el servidor: lo primero cuesta dos consultas por petición —una a la central— y lo segundo mete estado que se complica al escalar a varias instancias. El precio aceptado es que revocar un permiso tarda hasta la vigencia del token en surtir efecto. En la API se validan con una policy `RequierePermiso("rentas.autorizar")`.

**El permiso efectivo es una intersección, no una lectura:** `permisos del rol ∩ módulos del plan del tenant`. Un usuario con `logistica.crear` en una empresa cuyo plan no incluye logística no puede crear un flete.

### 6.1 Decisiones de autenticación

| Tema | Decisión | Razón |
|---|---|---|
| Hashing | **PBKDF2-HMAC-SHA256**, 600 mil iteraciones, del paquete `Microsoft.AspNetCore.Cryptography.KeyDerivation` | Argon2id es la primera recomendación de OWASP, pero en .NET solo existe en paquetes de terceros, y una dependencia de criptografía de terceros es la categoría que más cuidado exige auditar. 600 mil iteraciones sigue siendo aceptable |
| Formato del hash | Autodescriptivo: `pbkdf2-sha256$iteraciones$sal$clave` | Subir el costo no invalida ni un hash existente: los viejos se verifican con sus propios parámetros y se rehashean en el siguiente login, que es el único momento en que se tiene la contraseña en claro |
| Firma | HMAC-SHA256 con llave simétrica de ≥32 bytes, desde secretos | El emisor y el validador son el mismo proceso. La llave nunca se commitea |
| Audiencias | **Dos, separadas:** `maquinaria-plataforma` y `maquinaria-empresa` | No es cosmética: con una sola, un token de superadministrador serviría en un endpoint de empresa, porque los firma la misma llave. Cada endpoint exige la suya con una policy sobre el claim `ambito` |
| Contenido del token | `sub`, `email`, `name`, `ambito`. **Nunca `nombre_bd`** | Un JWT va firmado pero **no cifrado**: cualquiera que lo tenga lee su contenido. Los nombres de las bases de los clientes no viajan al navegador |
| Nombres de claim | Cortos (`sub`, `email`, `name`) y `MapInboundClaims = false` | Por defecto JwtBearer traduce los nombres estándar a los URIs de WS-Federation, así que el token dice `sub` y el código que lo lee no lo encuentra. Son además ~55 bytes de relleno por claim en cada petición |
| Tolerancia de reloj | `ClockSkew = TimeSpan.Zero` | Los cinco minutos por defecto alargarían de más la vida de cada token, y aquí emisor y validador son el mismo proceso |
| Límite de intentos | Por IP, con el limitador nativo de .NET | El limitador corre **antes** de leer el cuerpo, así que no puede particionar por correo. El límite por correo —y por slug, con el login de empresa— necesita estado y va en el caso de uso |
| Respuesta al fallo | Un solo mensaje para las tres causas, y **tiempo constante** con hash señuelo | Distinguir "no existe el correo" de "contraseña incorrecta" regala la lista de cuentas. Y responder de inmediato cuando no existe, frente a ~130 ms cuando sí, es una diferencia medible que la revela igual |


Cada tenant tiene sus propios roles: los 9 del documento son una **semilla** que se copia al crear el tenant, y luego cada empresa los ajusta. Un rol no es global.

**Rol `cliente`:** es un usuario externo con acceso a un portal restringido (sus rentas, sus saldos, sus documentos). Vive en el mismo `usuario` con un `ClienteId` asociado y permisos mínimos.

---

## 7. Auditoría

Un `SaveChangesInterceptor` de EF Core registra automáticamente en `auditoria`:

`usuario_id`, `fecha_utc`, `entidad`, `entidad_id`, `accion`, `valores_anteriores jsonb`, `valores_nuevos jsonb`, `ip`, `origen`

Usar `jsonb` (no columnas fijas) permite auditar cualquier entidad sin cambiar el esquema, y consultar con operadores de Postgres:

```sql
SELECT * FROM auditoria
WHERE entidad = 'Renta' AND valores_anteriores ? 'TarifaDiaria';
```

Se excluyen del interceptor las tablas de alto volumen y bajo valor auditor (`lectura_horometro`, `auditoria` misma, evidencias).

---

## 8. Almacenamiento de archivos

Este sistema es intensivo en multimedia: cada renta genera una inspección de salida y una de devolución, cada una con fotografías de motor, llantas, carrocería, hidráulico, horómetro y daños. Estimando 20 fotos por inspección, 40 por renta — con 100 equipos rotando mensualmente son ~48,000 imágenes al año **por empresa**. Es el mayor costo de infraestructura del producto y el primer cuello de botella.

```csharp
public interface IAlmacenamientoArchivos
{
    Task<string> GuardarAsync(Stream contenido, string ruta, string tipoMime, CancellationToken ct);
    Task<Stream> ObtenerAsync(string ruta, CancellationToken ct);
    Task<Uri> ObtenerUrlFirmadaAsync(string ruta, TimeSpan vigencia, CancellationToken ct);
    Task EliminarAsync(string ruta, CancellationToken ct);
}
```

Implementaciones: `AlmacenamientoDisco` (dev) y `AlmacenamientoS3` (prod, compatible con AWS S3, Cloudflare R2 y MinIO).

### 8.1 Envío de correo

Misma forma y misma razón: un cliente on-premise usará su propio SMTP, no el servicio que usemos nosotros.

```csharp
public interface IEnviadorCorreo
{
    Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct);
}
```

Implementaciones: `CorreoEnLog` (dev — escribe el mensaje en el log y no manda nada) y **`CorreoResend`** (nube). Se elige con `Correo:Proveedor`, igual que el almacenamiento.

**Resend, con `HttpClient` tipado y sin paquete de NuGet.** La API que necesitamos es **un** endpoint —`POST /emails`— y un SDK de terceros para eso es una dependencia más que pinear, auditar y actualizar a cambio de ahorrar veinte líneas. Mismo criterio que descartó MediatR y un paquete de Argon2.

**El resultado se devuelve, no se lanza.** `EnviarAsync` no tira excepción cuando falla, y eso es deliberado: si el aprovisionamiento creó la base, la migró, sembró los roles y creó al administrador, que el correo no salga **no puede** convertir todo eso en un fracaso. La operación que lo pidió decide qué hacer; en el alta de empresas, se registra un error y se reporta `invitacionEnviada: false`.

**Limitación del sandbox de Resend, mientras el dominio no esté verificado:** solo acepta `onboarding@resend.dev` como remitente y solo entrega al correo del titular de la cuenta. No es un error de configuración. La verificación del dominio está pendiente junto con el registro del dominio (ver `maquinaria-frontend/docs/integracion-backend.md`).

`Resend:Llave` va en secretos —user-secrets en desarrollo, variables de entorno en Railway— y nunca se commitea.

Reglas:
- **Rutas siempre prefijadas por tenant:** `{tenantId}/equipos/{equipoId}/inspecciones/{inspeccionId}/{archivoId}.jpg`. El prefijo hace trivial calcular consumo por tenant, aplicar cuotas y borrar todo al dar de baja una empresa.
- Los archivos **nunca** se sirven a través de la API. Se entregan URLs firmadas con vigencia corta, para no pasar los bytes por el servidor de aplicación.
- Compresión y generación de miniaturas del lado del cliente (PWA de campo) antes de subir: la conectividad en obra es mala y subir una foto de 8 MB desde una excavadora no es opción.

---

## 9. Frontend

- **Standalone components** y `signals` para estado local; sin NgRx hasta que haya evidencia de que se necesita.
- **Lazy loading por módulo de ruta.** Con 26 módulos, un bundle único es inviable.
- **Cliente HTTP generado** desde el OpenAPI del backend, para que un cambio de contrato rompa la compilación del front en lugar de romper producción.
- **Interceptores:** JWT, refresh automático, manejo de errores, `tenant`.
- **PWA con soporte offline** en Fase 5 (módulo de campo). Se diseña desde el inicio con esto en mente: las inspecciones deben poder capturarse sin red y sincronizarse después, lo que implica IDs generados en el cliente (de ahí uuid v7) y resolución de conflictos.

---

## 10. Despliegue: consecuencias del stack elegido

Estas cinco cosas **no son detalles de operación**, son decisiones que cambian el código. Hay que respetarlas desde la primera línea.

### 10.1 Los dos endpoints de Neon

Neon expone dos, y hay que usar los dos:

| Endpoint | Para qué |
|---|---|
| **Pooled** (`...-pooler.neon.tech`) | Runtime de la API |
| **Directo** (`...neon.tech`) | Migraciones, `CREATE DATABASE` y aprovisionamiento |

El pooled corre **PgBouncer en modo transacción**: la conexión física vuelve al pool al terminar cada transacción. Eso descarta cualquier estado de sesión (`SET`, tablas temporales, `LISTEN/NOTIFY`) y también el DDL.

El endpoint directo es obligatorio para migraciones y para `CREATE DATABASE`, que además **no puede correr dentro de una transacción** — ver §2.4.

**Todas las bases de empresa viven en el mismo proyecto de Neon**, así que comparten cómputo y endpoint. La cadena de cada empresa es la central con el nombre de base cambiado. Un solo endpoint, N bases.

> Con base compartida, el modo transacción de PgBouncer obligaba a envolver **cada** petición en una transacción explícita, porque el RLS dependía de `SET LOCAL`. Al pasar a base por empresa esa obligación desaparece: las transacciones se usan donde el negocio las necesita, no como requisito de infraestructura.

### 10.2 Railway y Neon en la misma región

Cada consulta de EF Core es un viaje de red. Con Railway en `us-west` y Neon en `us-east` son ~60 ms por consulta; una pantalla con 20 consultas tarda 1.2 s solo en latencia. Ambos en **la misma región** (`us-east`). Es el factor de rendimiento número uno del sistema y no cuesta nada acertarle.

Complemento: Neon en plan gratuito **suspende el cómputo** tras unos minutos de inactividad, con un arranque en frío de cientos de milisegundos a segundos. Aceptable en desarrollo, no para una demo con cliente.

### 10.3 Cookies entre Cloudflare Pages y Railway: usar subdominios del mismo dominio

El refresh token va en cookie `HttpOnly` (no en `localStorage`, donde cualquier XSS lo roba). Pero Pages y Railway son orígenes distintos, así que la cookie sería de terceros y necesitaría `SameSite=None`, que los navegadores restringen cada vez más.

Solución, y es gratis porque el DNS ya está en Cloudflare:

```
app.tudominio.com   → Cloudflare Pages   (Angular)
api.tudominio.com   → Railway            (API, proxied)
```

Mismo dominio registrable, así que la cookie se emite para `.tudominio.com` y funciona con `SameSite=Lax`. **No usar los dominios que asignan Pages y Railway por defecto** (`*.pages.dev`, `*.up.railway.app`): son dominios registrables distintos y obligan a `SameSite=None`.

### 10.4 R2: sin egreso, con SDK de S3, y el tamaño se lleva en la base

R2 es la elección correcta precisamente por el volumen de fotos del sistema: **no cobra egreso**, que es lo que encarece S3 cuando cada expediente de equipo muestra decenas de imágenes.

- Se usa `AWSSDK.S3` apuntando el `ServiceURL` al endpoint de R2. No hace falta un SDK propio.
- Las URLs firmadas funcionan igual, así que el patrón de §8 (nunca servir bytes por la API) se mantiene.
- R2 **no** da un "tamaño total por prefijo" barato. Por eso el consumo de almacenamiento por tenant se lleva en la tabla `archivo` de nuestra base, no consultando el bucket.

### 10.5 Identificación de la empresa: campo explícito en el login

La pantalla de ingreso pide tres datos: **Empresa** (el `slug`), correo y contraseña.

Es obligatorio, no una preferencia de interfaz: con los usuarios viviendo en la base de su empresa, hay que saber **en cuál buscar antes de validar nada**. Las alternativas eran un subdominio por empresa —que exige DNS comodín y Cloudflare for SaaS sobre Pages— o un índice `correo → empresa` en la base central. El campo explícito gana porque **no duplica ni un correo fuera de su base**, que es justo lo que este modelo busca.

Flujo: normalizar el slug → buscar en `tenant` → verificar que puede operar → abrir `ContextoEmpresa` contra su base → validar credenciales → emitir el JWT, que ya lleva el identificador de la empresa para las peticiones siguientes.

Tres reglas para no filtrar información, detalladas en `05-esquema-fase0.md` §6:

- **Un solo mensaje de error.** Nunca distinguir entre empresa inexistente, correo inexistente y contraseña incorrecta: distinguir regala la lista de clientes.
- **Tiempo de respuesta constante.** Si la empresa no existe se responde de inmediato; si existe, hashear tarda ~200 ms. Esa diferencia es medible y revela quién es cliente. Hay que ejecutar siempre un hash señuelo.
- **Límite de intentos** por combinación de slug y correo, y por IP.

Migrar a subdominios después no rompe nada: solo agrega otra forma de resolver el mismo slug.

### 10.6 Dos repositorios: el contrato de la API debe gestionarse explícitamente

Decisión tomada: **dos repositorios independientes**, `maquinaria_back` (que contiene `docs/`) y `maquinaria_front`. Cada uno se conecta a su servicio: back → Railway, front → Cloudflare Pages. Ninguno necesita configuración de subcarpeta.

El costo de esta decisión es concreto y hay que administrarlo: **no existe el commit atómico**. Un cambio que toca las dos capas —lo normal, porque trabajamos en rebanadas verticales— son dos commits en dos repos que no pueden revertirse juntos. Tres medidas lo compensan.

#### a) El cliente HTTP se genera y **se commitea** en el front

.NET 10 expone el documento OpenAPI de forma nativa (`AddOpenApi()` / `MapOpenApi()` en `Microsoft.AspNetCore.OpenApi`, ya sin Swashbuckle). El front tiene un script:

```
npm run api:sync    →  genera src/app/core/api/ desde /openapi/v1.json
```

Los archivos generados **se versionan en el repo del front**. Es la parte importante: así el front compila sin necesitar el backend corriendo, y cualquier cambio de contrato aparece como un *diff* revisable en el historial del front. Es el sustituto práctico del commit atómico: no lo vuelve atómico, pero lo vuelve **visible**.

#### b) Evolución en tres pasos: expandir → migrar → contraer

Con despliegues independientes, un cambio incompatible rompe producción durante la ventana entre un deploy y el otro. Nunca se cambia un contrato de golpe:

| Paso | Repo | Acción |
|---|---|---|
| 1. Expandir | back | Agrega el campo o endpoint nuevo. **Conserva el viejo** |
| 2. Migrar | front | Regenera el cliente y adopta lo nuevo |
| 3. Contraer | back | Recién ahora elimina lo viejo |

Renombrar un campo y actualizar el front "al mismo tiempo" no existe: uno de los dos despliegues llega primero. La regla es que **cada despliegue debe ser compatible con la versión actualmente desplegada del otro repo**.

#### c) Trazabilidad del trabajo entre repos

Un GitHub Project que abarque los dos repositorios, y la convención de referenciar el issue del back desde el commit del front (`aperez/maquinaria_back#42`). Sin esto, dentro de seis meses no hay forma de saber qué cambio del front correspondía a cuál del back.

---

## 11. Modalidades de venta y despliegue

El modelo principal es suscripción, pero el negocio contempla también **vender licencia perpetua**. Eso no es solo un tema comercial: define reglas de arquitectura que hay que respetar desde el primer commit, porque romperlas después obliga a bifurcar el producto.

### 11.1 Las tres modalidades

| Modalidad | Dónde vive su base | Hospedaje | Qué implica |
|---|---|---|---|
| **SaaS estándar** | En nuestro proyecto de Neon, junto a las demás | Nosotros | El caso por defecto |
| **SaaS dedicado** | Proyecto o servidor propio | Nosotros | Solo cambia su cadena de conexión. Sin cambios de código |
| **On-premise** | Servidor del cliente | **El cliente** | Otro modelo operativo. Ver §11.3 |

Nótese que con el modelo de base por empresa, **la separación de datos ya es la misma en las tres**. Lo único que cambia entre modalidades es dónde corre esa base y quién la opera — por eso pasar de una a otra no toca código.

### 11.2 Las cinco reglas que hacen esto posible

Se respetan siempre, aunque hoy no haya ningún cliente on-premise. Son baratas ahora y carísimas de retrofitear.

**1. Una sola base de código. Nunca un fork "para on-premise."**
La diferencia entre modalidades es **configuración**, no código. Un fork significa mantener dos productos, y en seis meses uno de los dos queda atrás.

**2. Toda dependencia de la nube va detrás de una abstracción.**
`IAlmacenamientoArchivos` ya existe por esto (§8): en la nube resuelve a R2, en on-premise a disco local. Falta la misma abstracción para el envío de correo — un cliente on-premise usará su propio SMTP, no el servicio que usemos nosotros.

**3. Ninguna instalación puede depender de un servicio central nuestro para funcionar.**
Si el sistema necesita alcanzar un servidor nuestro para que la gente entre, un cliente on-premise queda a merced de su conectividad y de nuestra disponibilidad. **Esta regla es la razón definitiva por la que no hay un servicio de identidad central** (ver `04-pendientes.md` §5.1): un SSO hospedado por nosotros haría imposible el on-premise, que es justo lo que se quería habilitar.

**4. El multi-tenant se queda, aunque haya un solo tenant.**
Una instalación on-premise tendrá su base central con **una** fila en `tenant`, y la base de esa empresa. No se elimina la separación central/empresa ni el aprovisionamiento: quitarlos sería el fork de la regla 1, y el costo de dejarlos es cero.

Con el modelo de base por empresa esto sale casi gratis: entregar una instalación es entregar dos bases y el contenedor.

**5. Las migraciones nunca se aplastan después de un release.**
Un cliente on-premise puede estar dos versiones atrás y actualizar de golpe. Si se reescribe el historial de migraciones, su base ya no puede alcanzar a la nueva. El historial es append-only para siempre.

### 11.3 Lo que falta diseñar para on-premise

No es urgente —no hay cliente todavía— pero conviene saber qué implica antes de comprometerlo con alguien:

- **Artefacto de instalación.** Imagen Docker con la API, más instrucciones de Postgres. Ellos aportan servidor y base.
- **Licenciamiento.** Un archivo de licencia **firmado** —nombre del cliente, límites, fecha hasta la que hay soporte— verificado al arrancar con una llave pública embebida en la aplicación. Funciona sin red, no se puede falsificar sin la llave privada, y no depende de que el cliente esté en línea. Es preferible a la activación periódica, que rompe instalaciones sin internet.
- **Actualizaciones.** No podemos desplegar por ellos. Hace falta versionado semántico, notas de versión y un procedimiento de actualización que ellos ejecuten.
- **Soporte a ciegas.** No vemos sus registros ni su base. Hace falta un modo de exportar diagnóstico.
- **Fragmentación de versiones.** Un cliente en 1.2 y otro en 2.0 al mismo tiempo. Define hasta cuántas versiones atrás se da soporte.

### 11.4 Nota comercial

Una licencia perpetua sin cuota anual significa hospedar, actualizar y dar soporte para siempre a cambio de un pago único. Lo estándar en software empresarial es **licencia perpetua + mantenimiento anual** (típicamente 18-22 % del valor de la licencia), que cubre actualizaciones y soporte. Sin esa cuota, cada venta perpetua es un pasivo creciente.

---

## 12. Lo que deliberadamente NO hacemos al inicio

| No hacemos | Por qué |
|---|---|
| Microservicios | Un monolito modular bien organizado es lo correcto aquí. Los módulos están fuertemente acoplados por diseño (el documento lo dice: "los módulos no deberán funcionar de manera aislada") |
| Sistema contable completo | El propio documento lo advierte. Se integra con un PAC para CFDI |
| Event sourcing / CQRS estricto | Sobreingeniería para el volumen esperado. Vistas materializadas cubren los reportes |
| Kubernetes | Un contenedor en App Service / Cloud Run alcanza para los primeros cientos de tenants |
| Pasarela de pago de suscripciones | Fase posterior. Las entidades `Plan` y `Suscripcion` existen desde el inicio, el cobro automático no |
