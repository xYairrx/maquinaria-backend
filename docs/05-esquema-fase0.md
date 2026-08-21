# Esquema de base de datos — Fase 0 (Fundación)

> Modelo **multi-database**: una base central y una base por empresa.
> 5 tablas centrales + 10 tablas por empresa.
> Todo lo demás (equipos, clientes, rentas…) es Fase 1 y vive en la base de la empresa.

**Este archivo es un documento de diseño, no la fuente de la verdad.** Trabajamos *Code-First*: la verdad son las clases de C#, y las migraciones de EF Core generan el DDL. El SQL de abajo existe para razonar el diseño y para las partes que EF Core no sabe expresar (extensiones, constraints `EXCLUDE`, índices parciales).

---

## 1. Los dos mundos

| Mundo | Qué guarda | Base de datos |
|---|---|---|
| **Central** | El negocio del SaaS: qué empresas existen, qué contrataron, su estado de aprovisionamiento | Una sola |
| **Empresa (tenant)** | Todos los datos de negocio de ese cliente | Una por empresa: `maquinaria_<slug>` |

Cada empresa tiene **su propia base de datos**, creada y migrada automáticamente al darla de alta. El aislamiento es **físico**: no hay forma de que una consulta de una empresa alcance datos de otra, porque están en bases distintas.

### Consecuencia: dos `DbContext`

| Contexto | Base | Migraciones |
|---|---|---|
| `ContextoCentral` | La central | Su propio juego |
| `ContextoEmpresa` | La de cada empresa | Su propio juego, aplicado N veces |

`ContextoEmpresa` no lleva cadena de conexión fija: se construye en tiempo de ejecución con la de la empresa resuelta en el login.

### Lo que este modelo ELIMINA

Comparado con el diseño anterior de base compartida:

| Ya no existe | Por qué |
|---|---|
| `tenant_id` en cada tabla | Cada base tiene un solo cliente |
| Políticas de Row-Level Security | El aislamiento es físico |
| `FORCE ROW LEVEL SECURITY` | — |
| Interceptor de `SET LOCAL app.tenant_id` | — |
| Rol de base de datos separado para la app | — |
| La prueba de fuga entre tenants | No hay fuga posible |
| Transacción explícita obligatoria en cada request | Era requisito del `SET LOCAL` |
| `UNIQUE (tenant_id, correo)` | Basta `UNIQUE (correo)` |

Es una simplificación importante de toda la capa de datos.

---

## 2. Extensiones

En **cada** base de empresa:

```sql
CREATE EXTENSION IF NOT EXISTS btree_gist;  -- constraints EXCLUDE (Fase 1)
CREATE EXTENSION IF NOT EXISTS pg_trgm;     -- busqueda por texto parcial (Fase 1)
```

Van en la primera migración de `ContextoEmpresa`, así toda base nueva las trae. Verificado disponible en Neon: `btree_gist 1.8`, `pg_trgm 1.6`.

La base central también necesita `btree_gist`, por el `EXCLUDE` de `suscripcion`.

No hace falta extensión de UUID: se generan en C# con `Guid.CreateVersion7()`.

---

## 3. Base central

### `plan`

```sql
CREATE TABLE plan (
    id             uuid          PRIMARY KEY,
    codigo         text          NOT NULL,
    nombre         text          NOT NULL,
    descripcion    text          NULL,
    precio_mensual numeric(18,4) NOT NULL,
    moneda         text          NOT NULL DEFAULT 'MXN',
    orden          int           NOT NULL DEFAULT 0,
    activo         boolean       NOT NULL DEFAULT true,
    creado_en      timestamptz   NOT NULL DEFAULT now(),

    CONSTRAINT plan_codigo_unico  UNIQUE (codigo),
    CONSTRAINT plan_precio_valido CHECK (precio_mensual >= 0),
    CONSTRAINT plan_moneda_valida CHECK (length(moneda) = 3)
);
```

Un plan retirado se marca inactivo, no se borra: hay suscripciones históricas que lo referencian.

### `plan_limite`

```sql
CREATE TABLE plan_limite (
    id      uuid PRIMARY KEY,
    plan_id uuid NOT NULL REFERENCES plan(id) ON DELETE CASCADE,
    clave   text NOT NULL,   -- max_equipos, max_usuarios, max_sucursales, max_almacenamiento_gb
    valor   int  NOT NULL,   -- -1 = ilimitado

    CONSTRAINT plan_limite_unico UNIQUE (plan_id, clave),
    CONSTRAINT plan_limite_valor CHECK (valor >= -1)
);
```

**Por qué clave/valor y no columnas.** Agregar un límite nuevo no requiere migración ni desplegar. El costo es perder verificación de tipos: nada impide escribir `max_equipoz`. Se compensa con una clase de constantes en C# que sea el único lugar donde se escriben esas cadenas.

Es un intercambio que **solo vale la pena en tablas de configuración**. En tablas de negocio, clave/valor es un antipatrón.

### `tenant` — el catálogo de empresas

```sql
CREATE TABLE tenant (
    id                       uuid        PRIMARY KEY,
    slug                     text        NOT NULL,   -- lo que el usuario escribe al entrar
    nombre_bd                text        NOT NULL,   -- maquinaria_bajio
    razon_social             text        NOT NULL,
    nombre_comercial         text        NULL,
    rfc                      text        NULL,
    telefono                 text        NULL,
    correo_contacto          text        NULL,
    estado                   smallint    NOT NULL,   -- 1 Prueba | 2 Activo | 3 Suspendido | 4 Cancelado
    estado_aprovisionamiento smallint    NOT NULL,   -- 1 Pendiente | 2 Creando | 3 Lista | 4 Fallida
    version_esquema          text        NULL,       -- ultima migracion aplicada en su base
    zona_horaria             text        NOT NULL DEFAULT 'America/Mexico_City',
    moneda                   text        NOT NULL DEFAULT 'MXN',
    dia_pago                 smallint    NULL,       -- dia del mes de cobro
    creado_en                timestamptz NOT NULL DEFAULT now(),
    actualizado_en           timestamptz NULL,
    eliminado_en             timestamptz NULL,

    CONSTRAINT tenant_slug_unico   UNIQUE (slug),
    CONSTRAINT tenant_bd_unica     UNIQUE (nombre_bd),
    CONSTRAINT tenant_slug_formato CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$'),
    CONSTRAINT tenant_bd_formato   CHECK (nombre_bd ~ '^[a-z][a-z0-9_]{2,62}$'),
    CONSTRAINT tenant_dia_pago     CHECK (dia_pago IS NULL OR dia_pago BETWEEN 1 AND 31),
    CONSTRAINT tenant_moneda_valida CHECK (length(moneda) = 3)
);
```

Cuatro campos merecen explicación:

**`slug`** es lo que la persona escribe en el campo "Empresa" de la pantalla de login. Es el identificador público y estable; cambiarlo rompe el acceso de todos sus usuarios.

**`nombre_bd`** se deriva del slug reemplazando guiones por guiones bajos, porque un nombre de base con guiones obliga a entrecomillar en cada sentencia. El `CHECK` de formato **no es cosmético, es control de seguridad**: los identificadores SQL no se pueden parametrizar, así que `CREATE DATABASE` se arma concatenando. Sin esta validación, el nombre es un vector de inyección.

**`estado_aprovisionamiento`** existe porque insertar la fila y crear la base **no pueden ser atómicos** en PostgreSQL. Ante un fallo deja un registro reintentable, en lugar de un huérfano que haya que borrar a mano. Ver §5.

**`version_esquema`** guarda la última migración aplicada en la base de esa empresa. Sin esto, un fallo parcial al migrar deja versiones desalineadas de forma invisible.

### `suscripcion`

```sql
CREATE TABLE suscripcion (
    id        uuid        PRIMARY KEY,
    tenant_id uuid        NOT NULL REFERENCES tenant(id),
    plan_id   uuid        NOT NULL REFERENCES plan(id),
    inicio    timestamptz NOT NULL,
    fin       timestamptz NULL,       -- NULL = contrato indefinido
    estado    smallint    NOT NULL,   -- 1 Prueba | 2 Activa | 3 Vencida | 4 Cancelada
    creado_en timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT suscripcion_periodo_valido CHECK (fin IS NULL OR fin > inicio),

    CONSTRAINT suscripcion_sin_traslape
        EXCLUDE USING gist (tenant_id WITH =, tstzrange(inicio, fin) WITH &&)
        WHERE (estado IN (1, 2))
);
```

**Aquí aparece el patrón central del sistema, en su caso más simple.** Es el mismo mecanismo que en la Fase 1 impedirá rentar dos veces el mismo equipo, pero sobre algo fácil de razonar: una empresa no puede tener dos suscripciones vigentes a la vez.

Cómo leerlo:

- `EXCLUDE USING gist (...)` le dice a Postgres: rechaza cualquier fila nueva que, comparada con las existentes, cumpla **todas** las condiciones listadas.
- `tenant_id WITH =` → misma empresa. `tstzrange(inicio, fin) WITH &&` → los periodos se traslapan (`&&` es el operador de solapamiento).
- `WHERE (estado IN (1,2))` lo vuelve **parcial**: solo aplica a suscripciones de prueba o activas. Las vencidas y canceladas quedan en el historial sin estorbar.

Lo valioso es que lo garantiza el motor. Dos peticiones simultáneas no pueden crear ambas una suscripción: Postgres rechaza la segunda. Con un `if (existe) throw` en C#, las dos leerían "no existe" y las dos insertarían.

> **Dos columnas, no una columna `tstzrange`.** El tipo de rango solo se mapea en C# con `NpgsqlRange<T>`, de la librería Npgsql — y `Maquinaria.Dominio` no depende de infraestructura. Los constraints `EXCLUDE` aceptan **expresiones**, así que `tstzrange(inicio, fin)` es equivalente y deja el dominio limpio. Mismo criterio para `ocupacion_equipo` en la Fase 1.

### `usuario_plataforma` — superadmins (nosotros)

```sql
CREATE TABLE usuario_plataforma (
    id               uuid        PRIMARY KEY,
    correo           text        NOT NULL,
    hash_contrasena  text        NOT NULL,
    nombre           text        NOT NULL,
    activo           boolean     NOT NULL DEFAULT true,
    ultimo_acceso_en timestamptz NULL,
    creado_en        timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT usuario_plataforma_correo_unico UNIQUE (correo)
);
```

Con el modelo multi-database la separación es más natural que antes: los superadministradores viven en la base central y **no existen en ninguna base de empresa**. No hay forma de que un error de permisos dentro de una empresa alcance la plataforma.

---

## 4. Base de empresa

Estas 10 tablas se crean en **cada** base de empresa. Ninguna lleva `tenant_id`: la base entera es de un solo cliente.

### `usuario`

```sql
CREATE TABLE usuario (
    id                      uuid        PRIMARY KEY,
    correo                  text        NOT NULL,   -- normalizado a minusculas al escribir
    hash_contrasena         text        NULL,       -- NULL mientras la invitacion no se acepta
    nombre                  text        NOT NULL,
    apellidos               text        NULL,
    telefono                text        NULL,
    activo                  boolean     NOT NULL DEFAULT true,
    debe_cambiar_contrasena boolean     NOT NULL DEFAULT false,
    ultimo_acceso_en        timestamptz NULL,
    creado_en               timestamptz NOT NULL DEFAULT now(),
    actualizado_en          timestamptz NULL,
    eliminado_en            timestamptz NULL,

    CONSTRAINT usuario_correo_unico UNIQUE (correo)
);
```

**`UNIQUE (correo)` a secas.** Con base compartida esto tenía que ser `(tenant_id, correo)` para permitir que la misma persona trabajara en dos empresas. Con base por empresa el problema desaparece solo: son bases distintas, así que el mismo correo puede existir en varias sin conflicto y sin complicar la restricción.

**`hash_contrasena` es nullable** porque no hay registro público: los usuarios se crean por invitación. Entre que se crea la cuenta y la persona define su contraseña, la fila existe sin hash. Un usuario en ese estado no puede iniciar sesión.

El correo se normaliza a minúsculas **al escribir**, en la capa de aplicación. Es más simple y portable que `citext` o un índice sobre `lower(correo)`, y evita que `Juan@x.com` y `juan@x.com` sean dos cuentas.

> **Falta `cliente_id` y es a propósito.** El rol `cliente` —el usuario externo que ve *sus* rentas en un portal— necesita esa columna, pero la tabla `cliente` no existe hasta la Fase 1. Se agrega ahí, junto con la decisión de cómo filtrar filas dentro de una misma empresa (ver §6.1).

### `token_acceso` — invitaciones y restablecimiento

**No hay registro público.** Los tenants los da de alta un superadministrador; los usuarios se crean por invitación.

```sql
CREATE TABLE token_acceso (
    id            uuid        PRIMARY KEY,
    usuario_id    uuid        NOT NULL REFERENCES usuario(id) ON DELETE CASCADE,
    proposito     smallint    NOT NULL,   -- 1 Invitacion | 2 RestablecerContrasena
    hash_token    text        NOT NULL,
    expira_en     timestamptz NOT NULL,
    usado_en      timestamptz NULL,
    invalidado_en timestamptz NULL,
    creado_por_id uuid        NULL REFERENCES usuario(id),
    creado_en     timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT token_acceso_hash_unico UNIQUE (hash_token),
    CONSTRAINT token_acceso_vigencia   CHECK (expira_en > creado_en)
);
```

**Una tabla para dos propósitos.** Invitar a un usuario nuevo y restablecer la contraseña de uno existente son el mismo mecanismo —token de un solo uso con vigencia— y solo cambia la intención. Dos tablas serían duplicación.

**Por qué no mandar una contraseña temporal por correo.** Viajaría en texto plano y se quedaría en la bandeja del destinatario para siempre. Con el token, **la contraseña nunca viaja**: el usuario recibe una liga, la abre, y define su contraseña él mismo. La liga caduca y sirve una sola vez.

| Campo | Por qué |
|---|---|
| `hash_token` | Se guarda el hash, no el token. Leer la base no debe dar ligas usables |
| `usado_en` | Lo vuelve de un solo uso. Un token ya usado se rechaza aunque no haya caducado |
| `invalidado_en` | Al reenviar una invitación se invalida la anterior, para que no queden dos ligas válidas |
| `expira_en` en la fila | Una invitación puede durar días; un restablecimiento debe durar una hora |
| `proposito` | Impide que un token de restablecimiento sirva para aceptar una invitación |

**`creado_por_id` es nullable** porque una invitación la puede crear el administrador de la empresa —que está en `usuario`— o un superadministrador nuestro —que vive en la base central y **no existe aquí**—. `NULL` significa "la creó la plataforma".

### `permiso` — catálogo, sembrado en cada base

```sql
CREATE TABLE permiso (
    id          uuid PRIMARY KEY,
    clave       text NOT NULL,   -- 'equipos.editar'
    modulo      text NOT NULL,   -- 'equipos'
    accion      text NOT NULL,   -- 'editar'
    descripcion text NOT NULL,

    CONSTRAINT permiso_clave_unica UNIQUE (clave)
);
```

Los permisos son parte del **código**: existen porque hay un endpoint que los verifica. Ningún cliente inventa permisos. Con base por empresa, el catálogo se **siembra idéntico** en cada base durante el aprovisionamiento, y cada migración que agrega un módulo agrega también sus permisos.

Las 6 acciones: `consultar`, `crear`, `editar`, `eliminar`, `autorizar`, `exportar`.

### `rol`

```sql
CREATE TABLE rol (
    id          uuid        PRIMARY KEY,
    codigo      text        NOT NULL,
    nombre      text        NOT NULL,
    descripcion text        NULL,
    es_sistema  boolean     NOT NULL DEFAULT false,
    creado_en   timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT rol_codigo_unico UNIQUE (codigo)
);
```

Los 9 roles del módulo 25 —administrador, dirección, ventas, rentas, logística, taller, operador, cobranza, cliente— son una **semilla que se aplica al aprovisionar la base**, no un enum fijo. Cada empresa los renombra y ajusta: en una, "ventas" cotiza y autoriza; en otra, solo cotiza.

`es_sistema = true` marca los roles semilla, para impedir borrar el rol administrador y dejar la empresa sin acceso.

### `rol_permiso` y `usuario_rol`

```sql
CREATE TABLE rol_permiso (
    rol_id     uuid NOT NULL REFERENCES rol(id) ON DELETE CASCADE,
    permiso_id uuid NOT NULL REFERENCES permiso(id),

    PRIMARY KEY (rol_id, permiso_id)
);

CREATE TABLE usuario_rol (
    usuario_id uuid NOT NULL REFERENCES usuario(id) ON DELETE CASCADE,
    rol_id     uuid NOT NULL REFERENCES rol(id),

    PRIMARY KEY (usuario_id, rol_id)
);
```

Con base compartida estas tablas cargaban un `tenant_id` redundante solo para que la política de RLS fuera uniforme. Sin RLS, quedan limpias.

`usuario_rol` es N:N a propósito: en una empresa chica la misma persona es ventas y cobranza.

### `sesion_refresh` — refresh tokens con rotación

```sql
CREATE TABLE sesion_refresh (
    id                 uuid        PRIMARY KEY,
    usuario_id         uuid        NOT NULL REFERENCES usuario(id) ON DELETE CASCADE,
    hash_token         text        NOT NULL,
    expira_en          timestamptz NOT NULL,
    revocado_en        timestamptz NULL,
    reemplazado_por_id uuid        NULL REFERENCES sesion_refresh(id),
    ip                 inet        NULL,
    agente_usuario     text        NULL,
    creado_en          timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT sesion_refresh_hash_unico UNIQUE (hash_token)
);
```

Tres decisiones de seguridad, cada una resolviendo algo concreto:

1. **Se guarda el hash, no el token.** Si alguien lee la base, no obtiene sesiones usables. Mismo criterio que las contraseñas.
2. **`reemplazado_por_id` habilita detección de reuso.** Cada refresh emite un token nuevo y marca el anterior como reemplazado. Si llega un token ya reemplazado, alguien lo robó: se revoca **toda la cadena** y se obliga a iniciar sesión de nuevo.
3. **`ip` y `agente_usuario`** permiten mostrarle al usuario sus sesiones activas y cerrarlas.

### `archivo` — índice de lo que vive en R2

```sql
CREATE TABLE archivo (
    id              uuid        PRIMARY KEY,
    ruta            text        NOT NULL,   -- {slug}/equipos/{id}/inspecciones/{id}/{archivo_id}.jpg
    nombre_original text        NOT NULL,
    tipo_mime       text        NOT NULL,
    tamano_bytes    bigint      NOT NULL,
    hash_sha256     text        NULL,
    ancho_px        int         NULL,
    alto_px         int         NULL,
    subido_por_id   uuid        NULL REFERENCES usuario(id),
    creado_en       timestamptz NOT NULL DEFAULT now(),
    eliminado_en    timestamptz NULL,

    CONSTRAINT archivo_ruta_unica UNIQUE (ruta),
    CONSTRAINT archivo_tamano     CHECK (tamano_bytes > 0)
);
```

Existe desde la Fase 0 aunque las evidencias sean Fase 2, por cuatro razones:

- **Cuotas.** R2 no ofrece un "peso total por prefijo" barato. El consumo es `SUM(tamano_bytes)` en esta tabla.
- **Huérfanos.** Un archivo puede subirse y fallar el guardado del registro que lo usa.
- **Deduplicación.** `hash_sha256` evita volver a subir un manual de 40 MB que ya está.
- **Referencia única.** `evidencia` y `equipo_documento` apuntarán a `archivo.id`, no a una ruta suelta.

Aunque las bases estén separadas, **el bucket de R2 es compartido**, así que las rutas siguen prefijadas por el slug de la empresa: hace trivial calcular consumo, aplicar cuotas y borrar todo al dar de baja un cliente.

### `parametro` — configuración de la empresa

```sql
CREATE TABLE parametro (
    id    uuid     PRIMARY KEY,
    clave text     NOT NULL,
    valor text     NOT NULL,
    tipo  smallint NOT NULL,   -- 1 texto | 2 entero | 3 decimal | 4 booleano | 5 fecha | 6 json

    CONSTRAINT parametro_clave_unica UNIQUE (clave)
);
```

### `auditoria`

```sql
CREATE TABLE auditoria (
    id                 bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    usuario_id         uuid        NULL,
    fecha_utc          timestamptz NOT NULL DEFAULT now(),
    entidad            text        NOT NULL,
    entidad_id         text        NOT NULL,
    accion             smallint    NOT NULL,   -- 1 Alta | 2 Cambio | 3 Baja
    valores_anteriores jsonb       NULL,
    valores_nuevos     jsonb       NULL,
    ip                 inet        NULL,
    origen             text        NULL        -- 'api' | 'pwa' | 'sistema'
);
```

Dos rupturas deliberadas de las convenciones del proyecto:

- **`bigint` identity en vez de uuid v7.** Es la única tabla de altísimo volumen a la que nunca apunta una FK. Un entero secuencial es más compacto y más rápido de insertar.
- **Sin FK a `usuario`.** La auditoría debe sobrevivir al borrado de lo que audita. Además, cada FK es una verificación extra en cada insert, y esta tabla se escribe en cada operación.

`jsonb` en lugar de columnas fijas permite auditar cualquier entidad sin migrar el esquema:

```sql
SELECT * FROM auditoria
WHERE entidad = 'Renta' AND valores_anteriores ? 'TarifaDiaria';
```

Cuando crezca se particiona por rango de `fecha_utc`. No al inicio: es complejidad sin beneficio hasta los millones de filas.

---

## 5. Aprovisionamiento de una empresa

Dar de alta un cliente es una secuencia, no un `INSERT`:

```
1. INSERT en tenant                       → estado_aprovisionamiento = Pendiente
2. CREATE DATABASE maquinaria_<slug>      → Creando
3. Migraciones de ContextoEmpresa en esa base
4. Semillas: permisos, los 9 roles, parametros por defecto
5. Crear el primer usuario administrador (sin contrasena)
6. Emitir su token de invitacion y enviarlo
7. estado_aprovisionamiento = Lista, version_esquema = <ultima migracion>
```

### Los cuatro problemas de esta secuencia

**1. `CREATE DATABASE` no corre dentro de una transacción.** Es limitación de PostgreSQL, y EF Core envuelve en transacción por defecto. Hay que abrir una `NpgsqlConnection` directa contra la base central y ejecutar el comando fuera de transacción.

**2. El nombre de la base no se puede parametrizar.** Los identificadores SQL no aceptan parámetros, así que la sentencia se arma concatenando. Por eso el `CHECK` de `nombre_bd` es control de seguridad, y **la validación debe repetirse en C# antes de concatenar** — nunca confiar solo en la que hace la base.

**3. Los pasos 1 y 2 no son atómicos.** Si falla la creación de la base, la fila ya está insertada. Por eso existe `estado_aprovisionamiento`: el registro queda en `Fallida` y es **reintentable**, en lugar de ser un huérfano que haya que borrar a mano para poder reintentar.

**4. Es lento.** Crear una base y correr todas las migraciones tarda. Al inicio se hace en línea porque son pocos clientes; cuando estorbe, pasa a un `BackgroundService` y la interfaz consulta `estado_aprovisionamiento`.

### Migrar todas las empresas al desplegar

```
migrar-empresas              # todas
migrar-empresas --slug=x     # una sola
```

Recorre la tabla `tenant`, construye un `ContextoEmpresa` por cada base y aplica sus migraciones.

**Debe ser resistente a fallos parciales:** si falla en la empresa 23, las 22 anteriores ya migraron y las siguientes no. Por eso registra `version_esquema` por empresa y continúa en lugar de abortar. Un endpoint de salud reporta quién quedó atrasado — sin eso, el desfase es invisible hasta que algo truena.

**Regla derivada:** las migraciones **nunca se aplastan ni se reescriben** después de un release. Una empresa puede estar dos versiones atrás y tiene que poder alcanzar. El historial es *append-only* para siempre.

---

## 6. Ingreso al sistema

La pantalla de login pide **tres** datos:

```
Empresa      → el slug
Correo
Contraseña
```

Con los usuarios viviendo en la base de su empresa, hay que saber en cuál buscar **antes** de validar nada. El identificador de empresa lo resuelve sin duplicar ni un correo en la base central.

Flujo:

```
1. Normalizar el slug: trim y minusculas
2. Buscar en tenant (central) → obtener nombre_bd y estado
3. Verificar que el tenant puede operar (no suspendido ni cancelado)
4. Abrir ContextoEmpresa contra su base
5. Validar correo y contrasena
6. Resolver permisos y emitir el JWT
```

### Tres reglas para no filtrar información

**Un solo mensaje de error.** Nunca "esa empresa no existe" ni "el correo no existe". Siempre `Empresa, correo o contraseña incorrectos`. Distinguir le regala a cualquiera la lista de clientes.

**Tiempo de respuesta constante.** Si la empresa no existe se responde de inmediato; si existe, se hashea la contraseña y se tarda ~200 ms. Esa diferencia es medible y revela qué empresas son clientes. Hay que ejecutar siempre un hash señuelo aunque no haya nada que validar.

**Límite de intentos** por combinación de slug y correo, y por IP.

Para la fricción de recordar el identificador: la liga de invitación lo lleva prellenado, y se guarda en `localStorage` tras el primer ingreso.

### 6.1 Lo que sigue sin resolver: filtrado dentro de una empresa

El aislamiento por base responde *¿de qué empresa son estos datos?* Pero el rol **`cliente`** del módulo 25 necesita algo distinto: un cliente externo entra al portal y debe ver **solo sus propias rentas**, no las de los demás clientes de esa misma empresa. Eso está dentro de la misma base, así que ningún mecanismo actual lo cubre.

Opciones a evaluar en la Fase 1:

1. **Filtro en el caso de uso** — explícito y simple, pero olvidarlo una vez es una fuga entre clientes.
2. **Filtro global de EF Core** condicionado al `cliente_id` del usuario — automático, más difícil de razonar.
3. **Política de RLS** dentro de la base de la empresa, con `current_setting('app.cliente_id')` — garantizado por el motor. Irónicamente, el RLS que quitamos del multi-tenant reaparecería aquí, pero para otra cosa.
4. **Endpoints separados** para el portal de cliente — más código, superficie mínima.

**Sin decidir.** Depende de cómo queden `cliente` y `renta`.

---

## 7. Índices

Base central:

```sql
CREATE INDEX ix_tenant_estado      ON tenant (estado) WHERE eliminado_en IS NULL;
CREATE INDEX ix_suscripcion_tenant ON suscripcion (tenant_id);
```

`tenant.slug` y `tenant.nombre_bd` ya tienen índice por sus restricciones `UNIQUE`. El slug se consulta en **cada login**, así que ese índice es de los más usados del sistema.

Base de empresa:

```sql
CREATE INDEX ix_usuario_activos        ON usuario (activo) WHERE eliminado_en IS NULL;
CREATE INDEX ix_archivo_vigentes       ON archivo (creado_en DESC) WHERE eliminado_en IS NULL;
CREATE INDEX ix_auditoria_fecha        ON auditoria (fecha_utc DESC);
CREATE INDEX ix_auditoria_entidad      ON auditoria (entidad, entidad_id);
CREATE INDEX ix_sesion_usuario_activa  ON sesion_refresh (usuario_id) WHERE revocado_en IS NULL;
CREATE INDEX ix_token_acceso_pendiente ON token_acceso (usuario_id)
    WHERE usado_en IS NULL AND invalidado_en IS NULL;
```

Los índices **parciales** (`WHERE eliminado_en IS NULL`) son una ventaja de PostgreSQL que conviene explotar: con borrado lógico, casi todas las consultas quieren solo los registros vivos, y así el índice no carga con los borrados.

Nótese que ya no hay que anteponer `tenant_id` a cada índice compuesto. Otra simplificación del modelo multi-database.

---

## 8. Orden de las migraciones

**`ContextoCentral`:**

```
1. Extension               btree_gist (para el EXCLUDE de suscripcion)
2. plan, plan_limite
3. tenant
4. suscripcion
5. usuario_plataforma
6. Semilla de planes
```

**`ContextoEmpresa`** — se aplica a cada base nueva y a las existentes al desplegar:

```
1. Extensiones             btree_gist, pg_trgm
2. usuario
3. permiso, rol, rol_permiso, usuario_rol
4. token_acceso, sesion_refresh
5. parametro, archivo, auditoria
6. Semillas                permisos del sistema, los 9 roles y sus permisos por defecto
```

Los pasos de extensiones y semillas son SQL crudo dentro de migraciones (`migrationBuilder.Sql(...)`), porque EF Core no sabe expresar extensiones ni datos semilla condicionales.
