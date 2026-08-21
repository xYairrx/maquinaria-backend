# Esquema de base de datos — Fase 0 (Fundación)

> Modelo **multi-database**: una base central y una base por empresa.
> 9 tablas centrales + 10 tablas por empresa. **Las 19 están construidas y aplicadas.**
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

### `modulo` y `plan_modulo` — el plan es un conjunto de módulos

**Un plan no es un paquete de cupos, es un conjunto de módulos habilitados.** Los cupos cuelgan del tenant, en `tenant_limite`.

```sql
CREATE TABLE modulo (
    id          uuid     PRIMARY KEY,
    clave       text     NOT NULL,   -- 'logistica'  -> casa con permiso.modulo
    numero      smallint NOT NULL,   -- 8            -> el M8 del documento funcional
    nombre      text     NOT NULL,
    descripcion text     NULL,
    orden       int      NOT NULL,
    activo      boolean  NOT NULL,

    CONSTRAINT modulo_clave_unica  UNIQUE (clave),
    CONSTRAINT modulo_numero_unico UNIQUE (numero),
    CONSTRAINT modulo_numero_rango CHECK (numero BETWEEN 1 AND 99)
);

CREATE TABLE plan_modulo (
    plan_id   uuid NOT NULL REFERENCES plan(id) ON DELETE CASCADE,
    modulo_id uuid NOT NULL REFERENCES modulo(id),   -- RESTRICT

    PRIMARY KEY (plan_id, modulo_id)
);
```

`modulo` es un **catálogo de código**, igual que `permiso`: existe porque hay pantallas y endpoints que lo implementan, no porque un cliente lo invente. Se siembra por migración desde `ClavesModulo`.

`plan_modulo` va **sin `id` propio**, con llave compuesta, igual que `rol_permiso` y `usuario_rol`: nadie referencia una fila de esta tabla.

`numero` está separado de `orden` a propósito: el orden de presentación es una decisión comercial que puede cambiar, mientras que el número es la referencia estable al documento funcional y no cambia nunca.

**La consecuencia importante es de autorización.** Con los módulos definiendo el plan, hay dos compuertas en dos bases distintas:

```
¿el plan del tenant incluye 'logistica'?      -> central          (plan_modulo)
¿el rol del usuario tiene 'logistica.crear'?  -> base de empresa  (rol_permiso)
```

Y `permiso.modulo` es `text` en la base de la empresa, así que su relación con `modulo.clave` **no puede tener FK**: son bases separadas. Es una referencia blanda, y si alguien renombra una clave la compuerta deja de cerrar sin que nada truene. Dos consecuencias para el código:

- Los módulos contratados se resuelven **una vez, al iniciar sesión**, junto con `nombre_bd`, y viajan en el JWT o en caché. No se consulta la central en cada petición.
- Hace falta una **prueba en CI** que verifique que todo `permiso.modulo` sembrado existe como `modulo.clave`. Mismo criterio que la prueba de huérfanos de `evidencia`.

**Y una consecuencia de producto:** como el plan *es* su conjunto de módulos, un cliente que quiera un módulo extra necesita otro plan. Si ese caso se vuelve común hará falta un `tenant_modulo` de excepción, espejo de `tenant_limite`. Se deja fuera hasta que aparezca el caso real.

**El catálogo tiene los 26 módulos** que define la especificación funcional, sembrados en dos pasos: 18 en `CentralSemillaCatalogos` y los 8 restantes en `CentralModulosCompletos`, cuando el `.docx` entró al repositorio.

> **Son 26, no 30.** El documento numera hasta 30 pero salta el 21, 22, 23 y 28. Y cuatro nombres se corrigieron en esa segunda migración: M24 es *Sucursales y patios* (no "Configuración"), M25 *Usuarios y permisos*, M27 *Reportes*, y M29 ***QR de equipos*** — este último se había sembrado como "Campo" por el nombre de la Fase 5, cuando la PWA de campo es una fase, no un módulo.

### `tipo_limite` y `tenant_limite` — los cupos cuelgan del tenant

```sql
CREATE TABLE tipo_limite (
    id            uuid    PRIMARY KEY,
    clave         text    NOT NULL,   -- max_equipos, max_usuarios, max_sucursales, max_almacenamiento_gb
    nombre        text    NOT NULL,   -- 'Equipos'  -> comparador de planes
    descripcion   text    NOT NULL,
    unidad        text    NOT NULL,   -- 'equipos' | 'usuarios' | 'GB'
    valor_defecto int     NOT NULL,   -- -1 = ilimitado
    orden         int     NOT NULL,
    activo        boolean NOT NULL,

    CONSTRAINT tipo_limite_clave_unica UNIQUE (clave),
    CONSTRAINT tipo_limite_defecto     CHECK (valor_defecto >= -1)
);

CREATE TABLE tenant_limite (
    id             uuid PRIMARY KEY,
    tenant_id      uuid NOT NULL REFERENCES tenant(id) ON DELETE CASCADE,
    tipo_limite_id uuid NOT NULL REFERENCES tipo_limite(id),   -- RESTRICT
    valor          int  NOT NULL,

    CONSTRAINT tenant_limite_unico UNIQUE (tenant_id, tipo_limite_id),
    CONSTRAINT tenant_limite_valor CHECK (valor >= -1)
);
```

**Por qué sobre el tenant y no sobre el plan.** Un cliente que negocia 300 equipos con un plan de 200 obligaría a inventarle un plan a medida, y eso ensucia el catálogo comercial que se muestra al comparar. Separando *qué módulos* (plan) de *cuánto* (tenant), cada cosa cambia sin arrastrar a la otra.

**`tenant_limite` es dispersa a propósito:** solo guarda excepciones. Un tenant sin filas hereda `tipo_limite.valor_defecto`, que arranca en `-1`. Así el alta de una empresa no inserta ni una fila y nadie queda limitado por omisión. La cadena de resolución tiene dos niveles, no tres:

```
tenant_limite.valor  ->  tipo_limite.valor_defecto
```

`valor = 0` es válido y significa "no puede crear ninguno". Es **distinto** de no tener fila, que significa "usa el valor por defecto".

**Por qué el catálogo `tipo_limite` y no `clave` como texto libre.** Con texto libre, nada impide escribir `max_equipoz` y que el límite no se aplique nunca, en silencio. Con catálogo, la integridad la da el motor. Es el mismo criterio que `permiso`: la clave la define el código, pero vive en una tabla.

**Ojo con lo que el catálogo NO da.** Que el tipo de límite sea una fila no hace que un límite nuevo funcione sin desplegar: un límite solo acota cuando existe código que lo lee y bloquea la operación. `ClavesLimite` sigue siendo la fuente de la verdad y la semilla se genera desde ahí.

**Ni `valor_defecto` ni `activo` llevan `DEFAULT` en la base, y no es un olvido.** Con un `DEFAULT -1`, EF Core omitiría la columna al insertar un tipo con `ValorDefecto = 0` —0 es el valor sentinel de `int`— y un límite que quiso decir "cero permitido" se guardaría como ilimitado. Es la misma trampa que dejó `plan.activo` sin `DEFAULT`. El precio es que todo `INSERT` en SQL crudo debe darles valor.

**Y una advertencia para cuando se escriba la verificación:** el límite vive en la base **central** y el consumo vive en la base de la **empresa** —contar equipos, contar usuarios, `SUM(archivo.tamano_bytes)`—. No hay tabla de acumulados y no hay transacción que abarque las dos bases. Los límites del tenant se resuelven una vez, junto con `nombre_bd`, no en cada petición.

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

### `auditoria` — la bitácora de la plataforma

Misma tabla y misma entidad que la de la base de empresa: está documentada en §4. Hace falta **también aquí** porque dar de alta un tenant, suspenderlo, cambiarle el plan o moverle un `tenant_limite` ocurre solo en la central, y son las decisiones más privilegiadas del sistema. Sin esta tabla no quedaban registradas en ninguna parte.

### `usuario` — superadmins (nosotros)

```sql
CREATE TABLE usuario (
    id               uuid        PRIMARY KEY,
    correo           text        NOT NULL,
    hash_contrasena  text        NOT NULL,
    nombre           text        NOT NULL,
    activo           boolean     NOT NULL,
    ultimo_acceso_en timestamptz NULL,
    creado_en        timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT usuario_correo_unico UNIQUE (correo)
);
```

Con el modelo multi-database la separación es más natural que antes: los superadministradores viven en la base central y **no existen en ninguna base de empresa**. No hay forma de que un error de permisos dentro de una empresa alcance la plataforma.

**Homónima de la `usuario` de la base de empresa, a propósito.** Son la misma idea en dos mundos separados físicamente, y no hay colisión posible en SQL porque son bases distintas. En C# las distingue el *namespace*, y confundirlas no compila: cada una existe solo en su propio `DbContext`, así que pedirle un `DbSet<Plataforma.Usuario>` a `ContextoEmpresa` es un error de compilación, no un bug en producción.

---

## 4. Base de empresa

Estas 10 tablas se crean en **cada** base de empresa. Ninguna lleva `tenant_id`: la base entera es de un solo cliente.

**Las 10 están construidas y aplicadas en `maquinaria_plantilla`**, en tres migraciones: primero las 7 de autenticación y permisos, luego su semilla, y por último `parametro`, `archivo` y `auditoria`. El *append-only* no obliga a un solo golpe: obliga a no reescribir.

### `usuario`

```sql
CREATE TABLE usuario (
    id                      uuid        PRIMARY KEY,
    correo                  text        NOT NULL,   -- normalizado a minusculas al escribir
    hash_contrasena         text        NULL,       -- NULL mientras el estado es Invitado
    nombre                  text        NOT NULL,
    apellidos               text        NULL,
    telefono                text        NULL,
    estado                  smallint    NOT NULL,   -- 1 Invitado | 2 Activo | 3 Suspendido | 4 Baja
    debe_cambiar_contrasena boolean     NOT NULL,
    ultimo_acceso_en        timestamptz NULL,
    creado_en               timestamptz NOT NULL DEFAULT now(),
    actualizado_en          timestamptz NULL,

    CONSTRAINT usuario_correo_unico UNIQUE (correo),
    CONSTRAINT usuario_estado       CHECK (estado BETWEEN 1 AND 4)
);
```

**Los usuarios NO SE BORRAN: viven en un estado.** Por eso aquí no hay `activo` ni `eliminado_en`. El par original permitía cuatro combinaciones de las que dos eran basura —`activo` con `eliminado_en` puesto, e inactivo sin él, que no distinguía *por qué* no estaba activo—. Un solo `estado` las colapsa:

| valor | estado | ¿entra? | quién lo pone |
|---|---|---|---|
| 1 | `Invitado` | no | el alta. Sin contraseña, invitación vigente |
| 2 | `Activo` | **sí** | la persona, al aceptar la invitación |
| 3 | `Suspendido` | no | el administrador. Reversible |
| 4 | `Baja` | no | el administrador. No reversible |

Arranca en 1 y no en 0 por la misma convención que `EstadoTenant`: un enum de C# vale 0 por defecto, así que el 0 es detectablemente inválido y el `CHECK` lo hace cumplir Postgres.

`Invitado` explícito quita una inferencia frágil. Antes, "sin contraseña" se deducía de `hash_contrasena IS NULL`; ahora el login comprueba **un solo campo** en lugar de dos columnas y un hash.

**`hash_contrasena` sigue nullable** porque no hay registro público: los usuarios se crean por invitación, y entre que se crea la cuenta y la persona define su contraseña la fila existe sin hash.

**`UNIQUE (correo)` global, no parcial por estado.** Con base compartida esto tenía que ser `(tenant_id, correo)`; con base por empresa el problema desaparece solo. Pero como los usuarios no se borran, tiene una consecuencia que hay que aceptar a ojos abiertos: **un correo nunca se libera.** Si `ventas@empresa.com` fue de alguien que se dio de baja, no se le puede asignar a quien lo sustituya.

La alternativa —único solo entre los que no están de baja— volvería ambiguo el login: buscar por correo devolvería varias filas y habría que filtrar por estado *antes* de validar, y ese filtro, olvidado una vez, es un agujero de autenticación. Se queda global, y es regla escrita que los correos no se reciclan.

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
    id           uuid        PRIMARY KEY,
    codigo       text        NOT NULL,
    nombre       text        NOT NULL,
    descripcion  text        NULL,
    es_sistema   boolean     NOT NULL,   -- sembrado: no se puede borrar
    acceso_total boolean     NOT NULL,   -- salta la verificacion de permisos
    creado_en    timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT rol_codigo_unico UNIQUE (codigo)
);

-- Como maximo UNA fila con acceso_total: todas valen lo mismo, asi que el
-- unico parcial las limita a una. Impide crear un segundo rol privilegiado.
CREATE UNIQUE INDEX rol_acceso_total_unico ON rol (acceso_total) WHERE acceso_total;
```

Los 9 roles del módulo 25 —administrador, dirección, ventas, rentas, logística, taller, operador, cobranza, cliente— son una **semilla que se aplica al aprovisionar la base**, no un enum fijo. Cada empresa los renombra y ajusta: en una, "ventas" cotiza y autoriza; en otra, solo cotiza.

#### Las dos banderas, y por qué son dos

`es_sistema = true` marca los **nueve** roles semilla e impide borrarlos. Por sí sola **no concede nada**.

`acceso_total = true` es lo que hace que un rol **salte la verificación de permisos**, y va solo en `administrador`. Tienen que ser dos columnas: si `es_sistema` significara también "salta la verificación", los nueve la saltarían —ventas, operador y cliente incluidos— y la empresa quedaría abierta de par en par.

**Es una columna y no una comparación contra `codigo = 'administrador'`** porque las empresas renombran los roles. Si la verificación preguntara por la cadena, un rename legítimo dejaría a la empresa sin quién administre; y al revés, peor: alguien crearía un rol llamado `administrador` y se ganaría el poder sin que nadie se lo conceda.

#### El rol con acceso total es inmutable, y lo garantiza el motor

```sql
CREATE FUNCTION rol_proteger_sistema() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION
        'el rol de sistema con acceso total no se puede modificar ni borrar';
END $$ LANGUAGE plpgsql;

CREATE TRIGGER rol_sistema_inmutable
    BEFORE UPDATE OR DELETE ON rol
    FOR EACH ROW
    WHEN (OLD.es_sistema AND OLD.acceso_total)
    EXECUTE FUNCTION rol_proteger_sistema();
```

Ese rol no se puede editar, borrar, ni apagarle el acceso. Y como no se puede apagar, la regla *"debe quedar al menos un rol con acceso total"* **se cumple sola**, sin necesidad de un constraint diferido que la vigile.

El `WHEN` apunta a `es_sistema AND acceso_total`, no a `es_sistema` solo: los otros ocho lo traen y tienen que seguir siendo renombrables. Y solo referencia `OLD`, porque un trigger `BEFORE DELETE` no tiene `NEW`.

Esto no protege de un superusuario de Postgres. Sí protege de la aplicación, de un `ExecuteUpdate` distraído y del administrador de la empresa, que son los tres casos reales.

#### La contrapartida, asumida

El rol `administrador` **no aparece en la interfaz de asignaciones**: se otorga solo al aprovisionar la empresa. Eso significa que **la empresa tiene exactamente una persona con acceso total**. Si esa persona se va o pierde el acceso, nadie dentro de la empresa puede nombrar a otra — solo la plataforma, desde el panel de superadministrador.

Esa operación de recuperación tiene que existir, y se audita con `origen = 'plataforma'`.

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

> **Va en la SEGUNDA migración de `ContextoEmpresa`, no en la primera.** No participa en el login ni en la autorización, no tiene ni una FK, y hay una dependencia en el otro sentido: el interceptor no se puede escribir antes que la auth, porque necesita `usuario_id`, `roles`, `ip` y `origen`, que salen del contexto de la petición autenticada. La tabla sin interceptor no sirve de nada.
>
> **Y también hace falta en la base CENTRAL.** Dar de alta un tenant, suspenderlo, cambiarle el plan o moverle un `tenant_limite` ocurre solo allí, y hoy no queda registrado en ninguna parte. Son las decisiones más privilegiadas del sistema.

```sql
CREATE TABLE auditoria (
    id                 bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    correlacion_id     uuid        NOT NULL,
    fecha_utc          timestamptz NOT NULL DEFAULT now(),

    usuario_id         uuid        NULL,       -- sin FK, a proposito
    usuario_correo     text        NULL,       -- congelado al escribir
    roles              text[]      NOT NULL,   -- roles efectivos en ese momento
    origen             text        NOT NULL,   -- 'api' | 'pwa' | 'plataforma' | 'sistema'
    ip                 inet        NULL,

    accion             smallint    NOT NULL,
    entidad            text        NOT NULL,
    entidad_id         text        NOT NULL,
    valores_anteriores jsonb       NULL,
    valores_nuevos     jsonb       NULL,

    CONSTRAINT auditoria_accion CHECK (accion BETWEEN 1 AND 8)
);
```

Rupturas deliberadas de las convenciones del proyecto:

- **`bigint` identity en vez de uuid v7.** Es la única tabla de altísimo volumen a la que nunca apunta una FK. `GENERATED ALWAYS` y no `BY DEFAULT`: la aplicación **no puede suministrar un `id`**, así que no puede insertar en una posición arbitraria de la secuencia.
- **Sin FK a `usuario`.** No es solo el costo de verificación en la tabla más escrita: `usuario_id` puede apuntar legítimamente a una fila que **no existe en esta base** —un superadministrador vive en la central—, así que una FK no sería cara, sería **incorrecta**.

#### Los campos que el diseño original no tenía

| campo | qué pregunta responde |
|---|---|
| `correlacion_id` | *¿qué se hizo en una sola acción?* Crear una renta escribe `renta`, `renta_linea` y `ocupacion_equipo`. Su alcance es **la operación, no el `SaveChanges`** — una petición puede guardar varias veces, y las acciones 4 a 8 no pasan por `SaveChanges` |
| `usuario_correo` | *¿quién fue?* El correo puede cambiar, y para `origen = 'plataforma'` el `usuario_id` **nunca** va a resolver dentro de esta base |
| `roles` | *¿por qué se le permitió?* Los roles y `rol_permiso` cambian, así que no se puede reconstruir después. `'administrador' = ANY(roles)` dice si pasó por el bypass |

`correlacion_id` se genera **del lado del servidor**, una vez por unidad de trabajo, aunque el frontend mande un `X-Correlation-Id`: un id que viene del cliente es un id que el cliente puede repetir para atribuir sus filas al grupo de otra persona. El mismo valor va en el log estructurado, y es lo que permite cruzar una excepción técnica con las filas de auditoría de esa operación.

#### Los ocho valores de `accion`

```
1 Alta   2 Cambio   3 Borrado    ← las escribe el interceptor
4 Acceso                         ← consulto un expediente
5 Denegado                       ← intento rechazado por permisos
6 Exportacion                    ← se llevo datos
7 Login   8 LoginFallido
```

Un interceptor de `SaveChanges` **solo ve escrituras**, así que 4 a 8 las escribe el caso de uso a mano. Una exportación no modifica ni una fila, y es justo lo que quieres saber que hizo alguien con acceso total.

`3` se llama **`Borrado`** y no `Baja` a propósito: significa "la fila desapareció". La baja de un usuario es un cambio de estado, o sea `2 Cambio`; con el nombre viejo, dos cosas distintas compartían nombre en el campo que se consulta para saber qué pasó.

`5 Denegado` casi nunca disparará para `administrador` —salta la verificación— y existe para los otros ocho roles.

#### Lo que el interceptor NUNCA debe escribir

`usuario.hash_contrasena`, `token_acceso.hash_token` y `sesion_refresh.hash_token`. Si el interceptor serializa la entidad completa, esos hashes acaban aquí, en claro, para siempre — en la tabla que nunca se borra. Esas columnas guardan hashes precisamente para que leer la base no dé material usable, y la auditoría lo desharía por la puerta de atrás.

Hace falta una **lista de propiedades excluidas, declarativa**, y una prueba que falle cuando aparezca una propiedad nueva cuyo nombre huela a secreto (`hash`, `token`, `secreto`, `contrasena`) y no esté en la lista. Es lo único de la auditoría que, mal hecho, es peor que no tenerla.

Van solo las propiedades **que cambiaron**, no la entidad completa: por tamaño, y porque un diff de 40 campos donde cambió uno es ilegible. El `ChangeTracker` ya sabe cuáles son. En un `Alta` no hay subconjunto, así que `valores_nuevos` lleva la entidad entera menos las exclusiones.

#### Qué se audita, opt-in

**Opt-in por entidad, nunca opt-out.** Con 75 entidades, opt-out significa que cada entidad nueva se audita por accidente o se olvida en silencio.

| Se audita | No se audita | por qué no |
|---|---|---|
| `usuario`, `rol`, `rol_permiso`, `usuario_rol` | `sesion_refresh` | cada refresh sería una fila; el login ya se registra |
| `token_acceso` (sin el hash), `parametro` | `auditoria` | recursión infinita |
| Fase 1+: `renta`, `contrato`, `cotizacion`, `pago`, `tarifa` | `ocupacion_equipo` | derivada; se audita la renta que la causó |

#### Append-only en el motor

Con el administrador saltando la verificación, esta tabla es **el único** registro de lo que hizo. Un registro que el propio auditado puede borrar no es un registro.

```sql
CREATE FUNCTION auditoria_solo_insercion() RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'auditoria es append-only: % rechazado', TG_OP;
END $$ LANGUAGE plpgsql;

CREATE TRIGGER auditoria_inmutable
    BEFORE UPDATE OR DELETE OR TRUNCATE ON auditoria
    FOR EACH STATEMENT EXECUTE FUNCTION auditoria_solo_insercion();
```

**`TRUNCATE` va en la lista y no es redundante.** Un trigger de `UPDATE` y `DELETE` no lo intercepta, así que sin él un `TRUNCATE auditoria` vaciaría la bitácora entera sin despertar al trigger. Los triggers de `TRUNCATE` solo existen a nivel de sentencia, que es justo lo que este ya era.

`FOR EACH STATEMENT` y no `FOR EACH ROW`: no hace falta inspeccionar filas para rechazar la sentencia completa, y así un `DELETE` de un millón de filas se rechaza una vez en lugar de un millón.

#### La frontera con el log técnico

No hay columna `nivel`. Son dos sistemas y mezclarlos arruina los dos:

| | Auditoría | Log técnico |
|---|---|---|
| Registra | qué le pasó a los datos y quién lo hizo | errores, latencias, trazas |
| Vive en | esta tabla | salida estándar → Railway |
| Se borra | nunca | a las dos semanas |
| Para | el cliente, un auditor | nosotros, depurando |

> ¿Cambió datos, o alguien intentó algo y se le negó? → `auditoria`
> ¿Falló por una razón técnica? → log estructurado

Los fallos que **sí** son material de auditoría ya tienen su valor: `Denegado` y `LoginFallido`. Un 500 por una consulta lenta no es ninguno de los dos. Y la severidad para alertar es **derivada**, no capturada: sale de combinar `accion`, `roles` y `origen`.

#### Fuera a propósito

| Campo | Por qué no |
|---|---|
| `agente_usuario` | ya está en `sesion_refresh`, donde sirve para cerrar sesiones. Por fila de bitácora es ruido |
| `exito boolean` | redundante: `Denegado` y `LoginFallido` ya lo dicen |
| `motivo` / `comentario` | una bitácora registra hechos, no justificaciones. Si una acción necesita motivo, va en la entidad de negocio |
| cadena de hash entre filas | volvería la bitácora a prueba de alguien **con acceso a la base**, a costa de serializar las escrituras. Si un cliente lo exige, se agrega entonces |

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
CREATE INDEX ix_usuario_estado         ON usuario (estado);
CREATE INDEX ix_permiso_modulo         ON permiso (modulo);
CREATE INDEX ix_sesion_usuario_activa  ON sesion_refresh (usuario_id) WHERE revocado_en IS NULL;
CREATE INDEX ix_token_acceso_pendiente ON token_acceso (usuario_id)
    WHERE usado_en IS NULL AND invalidado_en IS NULL;

-- Segunda migracion, con auditoria
CREATE INDEX ix_archivo_vigentes       ON archivo (creado_en DESC) WHERE eliminado_en IS NULL;
CREATE INDEX ix_auditoria_fecha        ON auditoria (fecha_utc DESC);
CREATE INDEX ix_auditoria_entidad      ON auditoria (entidad, entidad_id);
CREATE INDEX ix_auditoria_usuario      ON auditoria (usuario_id, fecha_utc DESC);
CREATE INDEX ix_auditoria_correlacion  ON auditoria (correlacion_id);
```

Los índices **parciales** son una ventaja de PostgreSQL que conviene explotar: casi todas las consultas quieren solo los registros vigentes, y así el índice no carga con los demás.

`ix_usuario_activos` desapareció al sustituir `activo` + `eliminado_en` por `estado`: un índice sobre `estado` responde lo mismo sin filtro. `ix_permiso_modulo` es nuevo, y se usa al resolver la intersección con los módulos del plan.

**`eliminado_en` donde sigue existiendo —`tenant` y `archivo`— es baja lógica y nunca física: no hay `DELETE` en esas tablas.** En `archivo` además marca el momento en que dejó de existir el binario en R2, que es información que un estado no daría.

Nótese que ya no hay que anteponer `tenant_id` a cada índice compuesto. Otra simplificación del modelo multi-database.

---

## 8. Orden de las migraciones

**`ContextoCentral`:**

```
CentralInicial            APLICADA
  1. Extension            btree_gist (para el EXCLUDE de suscripcion)
  2. plan, modulo, plan_modulo
  3. tipo_limite
  4. tenant, tenant_limite
  5. suscripcion
  6. usuario

CentralSemillaCatalogos   APLICADA
  7. Semillas             modulo (18 conocidos), tipo_limite, plan de arranque

CentralAuditoria          APLICADA
  8. auditoria + trigger  auditoria_inmutable
```

**`ContextoEmpresa`** — se aplica a cada base nueva y a las existentes al desplegar:

```
EmpresaInicial            APLICADA en maquinaria_plantilla
  1. Extensiones          btree_gist, pg_trgm
  2. usuario
  3. permiso, rol, rol_permiso, usuario_rol
  4. token_acceso, sesion_refresh
  5. Trigger              rol_sistema_inmutable

EmpresaSemillaSeguridad   APLICADA
  6. Semillas             108 permisos (18 modulos x 6 acciones) y los 9 roles

EmpresaAuditoriaYConfiguracion   APLICADA
  7. parametro, archivo, auditoria
  8. Trigger              auditoria_inmutable
```

Los pasos de extensiones y semillas son SQL crudo dentro de migraciones (`migrationBuilder.Sql(...)`), porque EF Core no sabe expresar extensiones ni datos semilla condicionales.
