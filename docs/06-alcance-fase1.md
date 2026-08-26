# Alcance de la Fase 1 — el primer entregable

> **Este documento manda sobre el alcance del primer entregable.** El plan de las seis
> fases está en [`03-plan-desarrollo.md`](03-plan-desarrollo.md); aquí está qué entra, qué
> no, y con qué tablas.
>
> Se escribió el **2026-08-25** porque el alcance se había amendado tres veces en tres
> archivos distintos y ya no se podía leer de corrido. Si algo aquí contradice una nota
> fechada de otro documento, **manda esto**.

---

## 1. Qué se vende con este entregable

El criterio de salida no cambió y sigue siendo el que importa:

> El ciclo completo **cotizar → aprobar → rentar → cerrar** funciona, y es **imposible
> rentar dos veces el mismo equipo en fechas traslapadas**.

La segunda mitad es la que de verdad diferencia al producto, y la garantiza el motor de
base de datos —no código de aplicación— con un constraint `EXCLUDE` sobre
`ocupacion_equipo`. Ver §5.

---

## 2. Módulos que entran

| # | módulo | qué incluye |
|---|---|---|
| M25 | Usuarios y permisos | roles, matriz de permisos. **Construido** |
| M24 | Ubicaciones | bodegas, sucursales y patios, con transferencias de equipo |
| M2 | Equipos | catálogo, expediente, documentos, propósito renta/venta |
| M3 | Disponibilidad | el constraint de no-traslape |
| M4 | Clientes | contactos, domicilios y **obras** |
| M5 | Cotizaciones | propuesta comercial con estados |
| **M6** | **Contratos** | **con catálogo de cláusulas.** Era Fase 2; se adelantó |
| M7 | Rentas | la operación real, con máquina de estados |
| — | **Trabajadores y puestos** | **no está en la especificación.** Se agregó |
| — | **Compra y venta de equipo** | orden → autorizar → finalizar. Era Fase 3/fuera |
| — | **Tarifas** | catálogo de conceptos cobrables |

### Lo que sigue fuera

Logística completa (M8: vehículos, rutas, estados de entrega), las dos inspecciones
(M9, M10), evidencias y firmas (M11), horómetros (M12), todo el taller (M13–M18), pagos y
cobranza (M19), facturación (M20), notificaciones (M26), reportes (M27), QR (M29),
subrentas (M30) y el dashboard (M1).

---

## 3. Decisiones de alcance, con su historial

Varias se tomaron, se revirtieron y se volvieron a tomar. El historial importa: sin él,
alguien va a "corregir" el modelo de vuelta a una versión descartada.

| tema | decisión vigente | historial |
|---|---|---|
| **Operador en la renta** | **Sí puede incluirlo.** Solo *quién va* —un `trabajador`— y *cuánto se cobra*. Sin jornadas ni horas extra | se cerró como "no" y se reabrió el mismo día (§2.4 de [`04-pendientes.md`](04-pendientes.md)) |
| **Venta y compra de equipo** | **Dentro**, con proceso corto: orden → autorizar → finalizar | se habían acotado fuera y volvieron |
| **Contratos** | **Dentro**, con catálogo de cláusulas | eran M6, Fase 2 |
| **Ciclo de vida del equipo** | **Uno solo**, que puede terminar en venta. No hay parques separados de renta y de venta | firme desde el principio |
| **Flete** | Se cotiza **sobre la renta**, como línea con tarifa de flete | firme |
| **Reservas** | Solo desde usuarios internos. **Sin portal de cliente** | firme — pospone §6.1 de [`05-esquema-fase0.md`](05-esquema-fase0.md) |
| **Ubicaciones** | Tres tipos **al mismo nivel**, no jerarquía | se construyó como jerarquía y se corrigió |

---

## 4. Las cuatro formas que definen el modelo

### 4.1 Ubicación: tres tipos, capacidades derivadas

```
bodega     guarda máquinas
sucursal   administra y cotiza
patio      las dos cosas
```

**Una sola tabla** con `tipo`. Las dos capacidades —`AlmacenaEquipo`, `EsAdministrativa`—
**se derivan del tipo, nunca se capturan**. Con banderas que alguien escriba a mano se
podría crear una "bodega que cotiza", que no existe.

En la base viven como **columnas generadas**: se almacenan, pero las calcula Postgres.


```sql
ALTER TABLE ubicacion
    ADD COLUMN almacena_equipo boolean NOT NULL
        GENERATED ALWAYS AS (tipo IN (1, 3)) STORED,
    ADD COLUMN es_administrativa boolean NOT NULL
        GENERATED ALWAYS AS (tipo IN (2, 3)) STORED;
```

Con banderas normales, mantenerlas en sincronía con el tipo sería trabajo de la aplicación
y tarde o temprano una se queda atrás. Generadas, la fila incoherente **no se puede
escribir**. Y existen en la base —no solo en C#— para poder filtrar sin repetir
`tipo IN (1,3)` en cada consulta, y para que las reglas que cruzan tablas se apoyen en
ellas.

Tres reglas que **cruzan tablas** y por tanto ningún `CHECK` alcanza. Se harán cumplir con
un trigger que consulte esas columnas, cuando existan las tablas involucradas:

- un equipo solo puede estar en una ubicación que **almacene**;
- **un traspaso solo puede ir de una que almacene a otra que almacene** — nunca desde o
  hacia una sucursal;
- una cotización solo puede salir de una ubicación **administrativa**.

### 4.2 Tarifa: catálogo de conceptos cobrables

Una tarifa **no** es el precio de rentar un equipo por periodo. Es un **concepto que se
cobra**, y una renta o una venta puede arrastrar varios.

| tarifa | unidad |
|---|---|
| renta diaria | Día |
| mantenimiento | Evento o Mes |
| flete | Evento |
| operador | Día |
| maniobras | Evento |

La `unidad` —hora, día, semana, mes, **evento**, kilómetro— es lo que dice si el precio se
multiplica o se cobra una vez. Sin ella, "flete: 3500" es ambiguo.

Eso unifica tres cosas que si no tendrían tabla propia cada una:

- el flete se cotiza sobre la renta → línea con tarifa de flete;
- la renta incluye operador → línea con tarifa de operador **más el trabajador que va**;
- el mantenimiento se cobra → línea con tarifa de mantenimiento.

**El precio no vive en el catálogo.** Vive en `equipo_tarifa`, por equipo y con vigencia,
porque cambia con el tiempo y un cliente grande negocia el suyo.

### 4.3 Contrato: delgado, con las cláusulas fuera

El M6 lista como "información" del contrato *responsabilidades, combustible, daños,
penalizaciones*. **No son campos, son cláusulas.** Con el catálogo, el contrato se queda
con partes, fechas, depósito y estado, y los términos viven donde deben.

**El texto de la cláusula se congela al generar el contrato.** `contrato_clausula` copia
título y texto, y guarda `clausula_id` solo como referencia de dónde salió. Si mañana se
corrige la plantilla del catálogo, los contratos ya firmados **no cambian**.

Es el mismo principio que la tarifa congelada en la renta: nunca se lee el catálogo vigente
para reconstruir un documento pasado.

**Las cláusulas de un contrato pueden venir de dos lados**, y por eso `clausula_id` es
nullable:

- del **catálogo general** — las de responsabilidad y penalización suelen ser obligatorias;
- **propias, negociadas con ese cliente** — se redactan en el contrato y no existen en el
  catálogo. `clausula_id` va nulo y el texto es el único origen.

#### Estados, y la inmutabilidad

```
Borrador → Autorizado → Terminado        (+ Cancelado)
```

**Una vez autorizado, el contrato no se puede editar.** Ni él ni sus cláusulas. Es un
documento con firmas: si se pudiera cambiar el texto después, la firma no significaría
nada.

Y como es una garantía que no se puede confiar a la disciplina de quien escriba el
siguiente caso de uso, va en el motor — el mismo patrón que `rol_sistema_inmutable`:

```sql
CREATE TRIGGER contrato_inmutable
    BEFORE UPDATE OR DELETE ON contrato
    FOR EACH ROW
    WHEN (OLD.estado <> 1)          -- 1 = Borrador
    EXECUTE FUNCTION contrato_proteger_autorizado();
```

Con su gemelo sobre `contrato_clausula`, que consulta el estado del contrato padre. Cambiar
un contrato autorizado exige cancelarlo y hacer uno nuevo, que es lo que pasa en la
práctica.

### 4.4 Compra y venta: mismo flujo, simétrico

```
Borrador → Autorizada → Finalizada        (+ Cancelada)
```

Al **finalizar una compra**, el equipo se registra en el catálogo y queda a disposición de
renta o de venta. Al **finalizar una venta**, sale del parque y cierra su calendario de
ocupación, para que no pueda rentarse después.

Encabezado más detalle en las dos, con el mismo formato impreso.

---

### 4.5 Líneas y conceptos: por qué son dos tablas

Con un ejemplo se ve. Se rentan una excavadora y un compactador 10 días, con flete y
operador:

**`renta_linea` — lo que se renta.** Una fila por equipo:

| equipo | tarifa | cant. | precio | importe |
|---|---|---|---|---|
| EXC-001 | renta diaria | 10 | 4,500 | 45,000 |
| COM-004 | renta diaria | 10 | 1,800 | 18,000 |

**`renta_concepto` — lo que se cobra además.** No lleva equipo:

| tarifa | trabajador | cant. | precio | costo | importe |
|---|---|---|---|---|---|
| flete | — | 1 | 6,000 | 4,200 | 6,000 |
| operador | José Ramírez | 10 | 900 | — | 9,000 |

**Por qué separadas.** Responden preguntas distintas:

- *¿qué equipos van en esta renta?* → solo `renta_linea`, y es lo que genera las filas de
  `ocupacion_equipo`: dos equipos, dos filas de calendario;
- *¿qué se le cobra?* → las dos sumadas.

En una sola tabla, `equipo_id` tendría que ser nullable y toda consulta sobre equipos
tendría que acordarse de filtrar las filas de flete y operador. Separadas,
`renta_linea.equipo_id` es `NOT NULL` — una invariante de verdad, no una convención.

El `costo` de `renta_concepto` va aparte del importe cobrado porque el documento lo pide
explícitamente para el flete: el margen es la resta.

**`cotizacion_linea` es lo mismo en UNA tabla.** Una cotización no reserva nada, así que no
alimenta ningún calendario y no necesita el corte. Y puede referenciar un **tipo** en lugar
de un equipo concreto, para cotizar "una excavadora de 20 t" antes de saber cuál.

> **Un defecto corregido el 2026-08-25.** `cotizacion_linea` tenía un `CHECK` que exigía
> equipo o tipo en cada línea, y eso hacía **imposible cotizar un flete** — una línea de
> flete no tiene ninguno de los dos. Se quitó: la línea la define su `tarifa`, y el equipo
> es contexto opcional.

## 5. La pieza que sostiene todo: `ocupacion_equipo`

La regla más importante del sistema —*un equipo no puede tener dos rentas traslapadas*— y
la que el M3 describe como consultar *"rentas, reservas, mantenimiento, bloqueos y
traslados"*.

**No se implementa consultando cinco tablas.** Una sola tabla representa el calendario
físico del equipo, y todo lo que lo ocupa inserta una fila en ella:

| campo | |
|---|---|
| `equipo_id` | |
| `inicio`, `fin` | dos columnas, no un rango: `NpgsqlRange<T>` obligaría al dominio a depender de Npgsql |
| `motivo` | `Renta`, `Reserva`, `Mantenimiento`, `Traslado`, `Bloqueo`, **`Venta`** |
| `referencia_id` | a la renta, la orden de venta, la orden de trabajo… |
| `activo` | |

```sql
ALTER TABLE ocupacion_equipo
ADD CONSTRAINT ocupacion_sin_traslape
EXCLUDE USING gist (
    equipo_id              WITH =,
    tstzrange(inicio, fin) WITH &&
) WHERE (activo);
```

Lo que esto compra:

- **la doble asignación es imposible**, incluso con dos transacciones simultáneas. Un
  `if (existe) throw` en C# no lo logra: las dos leerían "no existe" y las dos insertarían;
- disponibilidad es **una consulta** con índice GiST, no cinco joins;
- *equipo en mantenimiento no está disponible* y *equipo fuera de servicio no se renta*
  salen gratis: son filas de ocupación;
- *una extensión revalida disponibilidad* se vuelve un `UPDATE` que el constraint valida
  solo;
- y con `motivo = Venta`, vender un equipo cierra su calendario sin que Rentas sepa nada de
  ventas.

`motivo = Venta` es nuevo, y es lo que conecta la venta de equipo con la garantía de
no-traslape.

**Esta tabla debe existir antes de la primera renta.** Si se agrega después hay que hacer
backfill de todas las rentas y arriesgarse a que el constraint falle sobre datos históricos
ya inconsistentes.

---

## 6. Inventario de tablas

Sobre las **10 de la Fase 0** que ya existen en cada base de empresa.

> **Las 28 están construidas y aplicadas** en `maquinaria_plantilla`, `maquinaria_demo` y
> `maquinaria_bajio` — las tres con la misma huella de esquema. Repartidas en tres
> migraciones: `EmpresaCatalogosFase1` (las 10 de catálogo), `EmpresaOperacionFase1` (las
> 18 de operación, más los dos `EXCLUDE` y los cinco triggers) y
> `EmpresaValoresPorDefectoRenglones`.
>
> El DDL de este documento y la base real se comparan automáticamente: **28 tablas, 307
> columnas, cero desajustes**. Aun así, la fuente de la verdad son las clases de C# y las
> migraciones, no este archivo — él no se ejecuta nunca.

### Catálogo y organización — 10

`categoria_equipo` · `tipo_equipo` · `marca` · `modelo_equipo` · `ubicacion` · `puesto` ·
`trabajador` · `proveedor` · `tarifa` · `clausula`

> **El DDL completo de las 28 tablas está en [`06-esquema-fase1.sql`](06-esquema-fase1.sql)**,
> con sus `CHECK`, los dos `EXCLUDE`, las columnas generadas y los cinco triggers. Ese
> archivo es documentación de diseño: la fuente de la verdad siguen siendo las clases de
> C# y las migraciones de EF Core.

### Operación — 18

| bloque | tablas |
|---|---|
| Clientes | `cliente` — **una sola tabla**, con su contacto y su domicilio dentro |
| Activos | `equipo`, `equipo_archivo`, `equipo_tarifa`, `transferencia_equipo` |
| | *`equipo_tarifa` es el precio **por equipo**: el catálogo dice qué se cobra, esta tabla cuánto* |
| **Ocupación** | **`ocupacion_equipo`** ← el `EXCLUDE` |
| Comercial | `cotizacion`, `cotizacion_linea`, `contrato`, `contrato_clausula` |
| Rentas | `renta`, `renta_linea`, `renta_concepto`, `extension_renta` |
| Compra y venta | `orden_compra`, `orden_compra_detalle`, `orden_venta`, `orden_venta_detalle` |

**Total de la Fase 1: 28 tablas.** La base de una empresa llega a **38**, y con las 9
centrales el sistema son **47**. (En `information_schema` se ven 39 por base: la de más es
`__EFMigrationsHistory`, que la crea EF Core.)

**Lo que NO existe todavía:** ni un endpoint, ni un caso de uso, ni una pantalla que use
estas 18 tablas. El esquema está completo y verificado; la implementación de la Fase 1 no
ha empezado.

### Ajustes del 2026-08-25 (tarde)

**El contrato cuelga de la renta, no de la cotización.** `contrato.renta_id` es
`NOT NULL` y **único**: un contrato por renta. `renta` perdió su `contrato_id` y `contrato`
perdió su `cotizacion_id`.

Eso invierte la cadena respecto a la especificación, que dice *"CONTRATO origina RENTA"*.
La cadena vigente es:

```
cotizacion  →  renta  →  contrato
```

Consecuencia de orden en el DDL: `contrato` y `contrato_clausula` se crean **después** de
`renta`, no antes.

> **Un contrato por renta** es mi lectura de "el contrato va sobre renta". Si un contrato
> marco debe amparar varias rentas, se quita el `UNIQUE (renta_id)` y ya.

**`equipo` pierde `proveedor_id`.** El proveedor vive en la orden de compra, y desde el
equipo se alcanza por join:

```
equipo → orden_compra_detalle → orden_compra → proveedor
```

Un dato en un solo lugar. Antes estaba duplicado, y dos copias del mismo hecho se
desincronizan.

> **Lo que esto deja pendiente:** `equipo.origen` sigue distinguiendo `Propio` de
> `Subrentado`, pero ya no hay forma de saber **de quién** viene un equipo subrentado — y
> una subrenta no se compra, así que la orden de compra no lo cubre. La subrenta es M30 y
> está fuera de la Fase 1, así que no estorba hoy; cuando entre, necesitará su propio
> enlace al proveedor.

### Simplificaciones del 2026-08-25

**`cliente` absorbe contacto y domicilio.** Se quitaron `contacto_cliente` y
`domicilio_cliente`; sus campos viven ahora dentro de `cliente`.

El precio es que **un cliente tiene un solo contacto y un solo domicilio**. Si mañana hace
falta el domicilio fiscal aparte del de entrega, o dos contactos —cobranza y operación—,
hay que volver a sacar la tabla y migrar los datos. Es una decisión del negocio, no un
descuido.

**`obra` desaparece.** Se sustituye por la descripción y la dirección **dentro de la
renta**: `lugar_descripcion` —obligatoria— más los campos de dirección, coordenadas y
contacto en sitio. `cotizacion` y `contrato` también perdieron su `obra_id`.

**La consecuencia que hay que aceptar:** el M27 de la especificación pide *"reportes de
rentabilidad: equipo, cliente, obra, sucursal y flete"*, y el documento dice que *"todas
las operaciones económicas relacionadas deben acumularse en el centro de costo
correspondiente"* — la obra era ese centro de costo. Con una descripción de texto libre, no
se puede agrupar rentabilidad por obra de forma confiable: "Torre Norte" y "torre norte"
son dos obras distintas para un `GROUP BY`.

Si esa rentabilidad se vuelve a pedir, la obra regresa como tabla. Lo que este cambio
compra a cambio es que capturar una renta no obliga a dar de alta una obra primero, que es
fricción real en el mostrador.

---

## 7. Sin cálculos: la Fase 1 captura importes

**Decidido el 2026-08-25, y desbloquea la fase.** En el primer entregable el sistema
**no calcula precios**: los costos y los valores se capturan.

| el sistema sí | el sistema no |
|---|---|
| multiplica cantidad por precio unitario | escoge la tarifa más conveniente |
| suma las líneas de un documento | decide si 12 días son semana + días |
| | calcula horas excedentes |
| | prorratea una extensión |
| | resuelve si los días son naturales o hábiles |

Eso deja las ocho preguntas de tarificación de [`04-pendientes.md`](04-pendientes.md) §1.2
**abiertas pero ya no bloqueantes**: eran todas sobre reglas de cálculo. Un vendedor
captura el importe que acordó y el documento lo conserva.

**Lo que hay que hacer bien desde ahora, aunque no se calcule:** congelar el precio
aplicado en la línea del documento. Si mañana se automatiza el cálculo, las cotizaciones y
rentas viejas tienen que seguir mostrando lo que se cobró, no lo que hoy daría la fórmula.

Siguen abiertos, y para después: los **depósitos** —cuándo se cobran, dónde se registran
mientras están retenidos, contra qué se aplican al cierre— y el **combustible**.

---

## 8. Cómo se migran todas las bases

Con una base por empresa, un despliegue **no termina cuando la migración está escrita**:
termina cuando las N bases la tienen.

### El comando

```bash
dotnet run --project src/Maquinaria.Api -- migrar-empresas
dotnet run --project src/Maquinaria.Api -- migrar-empresas --slug=bajio
```

Recorre `tenant`, abre la base de cada empresa con la cadena **directa**, aplica lo
pendiente y registra `version_esquema`. Reporta **una línea por empresa**:

```
  EMPRESA                   RESULTADO   DETALLE
  ------------------------  ----------  --------------------
  bajio                     MIGRADA     1 migraciones
  demo                      MIGRADA     1 migraciones
  norte                     omitida     Aprovisionamiento Fallida

  3 empresas: 0 al dia, 2 migradas, 1 omitidas, 0 fallidas.
```

Cuatro decisiones que lo hacen usable de verdad:

| decisión | por qué |
|---|---|
| **Continúa ante un fallo** | si truena en la empresa 23, las 22 anteriores ya migraron y las siguientes tienen derecho a intentarlo. Abortar dejaría un desfase peor |
| **Omite las que no están `Lista`** | migrar una base a medio aprovisionar da errores de "relation does not exist" en lugar de un mensaje claro. Eso lo arregla el alta reintentable, no el migrador |
| **Código de salida ≠ 0 si algo falla** | en un despliegue automatizado, que una de veinte bases quede atrás tiene que romper la tubería, no pasar desapercibido. `1` = alguna empresa falló, `2` = ni se pudo leer la lista |
| **Corre con el mismo contenedor que la aplicación** | usa la misma resolución de conexiones. Un comando con su propio arranque sería un segundo camino de código que puede divergir del que corre en producción |

### El endpoint de salud

```
GET /api/plataforma/salud/esquemas
```

Devuelve la versión aplicada de cada empresa, cuántas están atrasadas y —una sola vez, porque
es la del binario que responde— la versión disponible en el código. **Lee el historial de
migraciones de cada base, no `tenant.version_esquema`** — ese campo es una copia y puede estar
desactualizado; el historial de la base es la verdad.

Sin esto el desfase es **invisible** hasta que algo truena: una base dos versiones atrás
funciona bien hasta que alguien abre la pantalla que necesita la tabla nueva.

### La plantilla se migra aparte

`maquinaria_plantilla` no es un tenant, así que el comando no la toca. Va con
`dotnet ef database update --context ContextoEmpresa`.

---

## 9. Nota de operación

Al 2026-08-25 la credencial de Neon en `user-secrets` no autentica
(`28P01: password authentication failed`), así que **nada de la Fase 1 está aplicado ni
verificado contra la base real**. El código compila y las migraciones se generan, pero eso
no es lo mismo.

Y cuando vuelva la credencial: **hay que borrar y recrear `maquinaria_plantilla`**, no
migrarla. La migración de catálogos se reescribió tres veces y la plantilla conserva
estructura y filas de historial de versiones que ya no existen.
