# Plan de desarrollo — Fase 1 (backend)

> **Este documento manda sobre CÓMO se construye la Fase 1.** El **qué** entra y el **qué no**
> lo manda [`06-alcance-fase1.md`](06-alcance-fase1.md); las reglas de nombres y las
> invariantes, [`guias/convenciones.md`](guias/convenciones.md).
>
> Escrito el **2026-08-26**. Cubre **solo el backend**: ni una plantilla, ni una pantalla, ni
> un componente de Angular. El frontend consume `/openapi/v1.json` cuando esta fase cierre.
>
> **Revisado el 2026-08-26 (tarde)** contra `develop` en `9c47860`, después de las ocho
> confirmaciones que entraron ese día. Lo que cambió está en §1.1.

---

## 1. Punto de partida, verificado contra el repo

No contra los documentos. Medido el 2026-08-26 sobre `9c47860`:

| | |
|---|---|
| Archivos `.cs` en `src/` | **219** en 4 proyectos |
| `Maquinaria.Dominio` | **78 archivos** — las 38 tablas ya modeladas, con sus enums |
| Configuraciones de EF | **47** (9 centrales + 38 de empresa), una por entidad |
| Migraciones | **5 centrales** + 7 de empresa, aplicadas y verificadas contra Neon |
| Endpoints existentes | **19** (8 GET, 10 POST, 1 PATCH) — todos de plataforma y de acceso |
| Casos de uso de negocio | **0** |
| Endpoints de negocio | **0** |
| Transiciones de estado escritas | **0** |

Las entidades son **anémicas a propósito**: `Renta` tiene 20 propiedades y ningún método. Los
enums de estado existen; nadie los mueve. **Eso es el trabajo de esta fase.**

### 1.1 Lo que llegó el 2026-08-26 y afecta a este plan

Ocho confirmaciones sobre validación del alta, reenvío de invitaciones y la fusión de las dos
implementaciones de `migrar-empresas`. **Once archivos nuevos**, y tres de ellos cambian el
plan:

| Archivo nuevo | Qué es | Efecto en la Fase 1 |
|---|---|---|
| **`Dominio/Comun/FormatoRfc.cs`** | patrón del RFC mexicano, 12 o 13 caracteres, con `Ñ` y `&` | **lo usan `Cliente.Rfc` y `Proveedor.Rfc` (R5)**. La columna es `text` nullable y **sin `CHECK`**: este validador es la única defensa |
| **`Dominio/Comun/FormatoTelefono.cs`** | solo dígitos, sin separadores | **lo usan `Cliente`, `Proveedor`, `Trabajador` y `Ubicacion` (R3, R4, R5)**. Tampoco hay `CHECK` |
| **`Dominio/Comun/FormatoCorreo.cs`** | forma mínima `local@dominio.tld`, máximo 254 | **lo usa `Cliente.Correo` (R5)** y cualquier contacto que se capture |
| `Aplicacion/Empresas/ReenviarInvitacion.cs` | caso de uso nuevo | un endpoint más que migrar en R0 |
| `Migraciones/Central/…CentralInvitacionEnviada` | `Tenant.InvitacionEnviada` | las migraciones centrales pasan de 4 a 5 |
| 5 archivos de pruebas | `AltaEmpresaValidacion`, `FormatoRfc`, `FormatoTelefono`, `FormatoCorreo`, `ReenvioInvitacion` | `Api.Tests` pasa de 13 a **18 archivos** |

**La carpeta `Dominio/Comun/` es el hallazgo importante**, y hay que respetarla en lugar de
reinventarla: el criterio con el que se creó —un formato que viven varias entidades no pertenece
a la carpeta de ninguna— es exactamente el que van a necesitar `Cliente`, `Proveedor`,
`Trabajador` y `Ubicacion`. **Ninguna de esas columnas tiene `CHECK` en la base**, así que lo que
no se rechace ahí se guarda tal cual y no se vuelve a detectar. Ver §7 #5.

**Y `migrar-empresas` ya se corrió contra Neon** —esa corrida fue la que destapó sus defectos y
dejó `demo` y `bajio` al día—, así que el pendiente de R12 dejó de ser "correrlo por primera
vez" y pasó a ser "volver a correrlo después de la migración de §6".

### 1.2 Ajustes aplicados el 2026-08-26, antes de empezar

Dos cosas del JWT que estaban a medias y se cerraron para no arrastrarlas:

- **La llave se valida al arrancar.** `AddOptions<OpcionesJwt>()` ahora lleva `.Validate(...)`
  midiendo los 32 bytes, con `ValidateOnStart()`. Antes el proceso levantaba, contestaba `/salud`
  y reventaba en el **primer login**, porque `ProveedorTokensJwt` es singleton y solo se inyecta
  en los dos casos de uso de acceso. En un despliegue con `Jwt__Llave` mal puesta eso es un
  servicio que parece sano y no deja entrar a nadie. **No estorba a `migrar-empresas`:** ese
  camino crea un ámbito de `app.Services` y nunca arranca el host.
- **`Jwt:MinutosEmpresa` (15) y `Jwt:DiasRefresco` (30) pasaron a `appsettings.json`.** Vivían
  solo como valor por defecto en `OpcionesJwt`, así que no se podían ajustar por entorno sin
  recompilar. Mismos valores, cero cambio de comportamiento.

Y se corrigieron tres afirmaciones falsas: el comentario de `RegistroInfraestructura` que decía
que la llave reventaba al arrancar —ahora ya es verdad—, la fila de `Jwt:MinutosEmpresa` de
`configuracion.md` que decía que no estaba en `appsettings.json`, y el default de
`Correo:Remitente`, que seguía documentado como `onboarding@resend.dev` cuando el archivo dice
`no-reply@maqvia.com`.

Lo que **no** se tocó, a propósito: dos audiencias con policy por endpoint,
`MapInboundClaims = false`, `ClockSkew = Zero`, y la llave fuera de `appsettings.json`.

### Tres huecos transversales que bloquean

| Hueco | Consecuencia si se ignora |
|---|---|
| **No existe autorización por permiso** | los 108 permisos están en la base y viajan en el claim `perm` del JWT, pero **ningún endpoint los verifica**. Hoy solo se exige la policy de ámbito (`empresa` / `plataforma`) |
| **`IAlmacenamientoArchivos` no existe** | ni la interfaz. `equipo_archivo` y el expediente del equipo no se pueden implementar |
| **El interceptor de auditoría no existe** | las dos tablas `auditoria` están construidas y **vacías**. Todo lo que haga esta fase no queda auditado en ninguna parte |

Los tres se resuelven en la **rebanada 1**, antes del primer módulo de negocio. Escribir 50
controladores y después meter el filtro de permisos obliga a volver a tocar los 50.

---

## 2. Las tres decisiones de estructura

Cerradas el 2026-08-26. Reemplazan lo que decía `guias/convenciones.md` §"Organización por
módulo", que proponía carpetas por módulo (`Aplicacion/Equipos/`, `Aplicacion/Rentas/`).

| # | Decisión | Alternativa descartada |
|---|---|---|
| 1 | **Carpeta técnica arriba, módulo dentro**: `ObjetosDTO/Rentas/`, `IServicios/Rentas/` | módulo arriba, carpeta técnica dentro |
| 2 | **Controladores MVC**, no Minimal API | seguir con `MapGroup` + `Mapear<X>()` |
| 3 | **Lo existente se migra** al formato nuevo | dejar los 43 archivos actuales y que solo la Fase 1 use el formato nuevo |

**Por qué controladores, en una línea:** `[RequierePermiso("rentas.autorizar")]` es un atributo
declarativo que se lee en la firma del método; con Minimal API sería un `IEndpointFilter` que
hay que recordar encadenar en cada uno de los ~50 endpoints, y el que se olvide queda abierto
sin que nada lo detecte.

**El costo de la decisión 1, dicho para que nadie se sorprenda:** con 11 módulos en esta fase y
26 en el sistema completo, `ObjetosDTO/` termina con más de cien archivos repartidos en
subcarpetas, y trabajar en un módulo obliga a abrir cuatro ramas del árbol que están lejos
entre sí. Se aceptó a cambio de que la carpeta diga qué tipo de cosa contiene.

---

## 3. La estructura, definitiva

```
src/Maquinaria.Dominio/                       MODELOS — ya escritos, NO se mueven
├── Activos/ Catalogos/ Comercial/ Compras/ Configuracion/
├── Organizacion/ Plataforma/ Seguridad/ Terceros/ Trazabilidad/
└── Comun/              FormatoRfc  FormatoTelefono  FormatoCorreo  ← formatos compartidos

src/Maquinaria.Aplicacion/
├── ObjetosDTO/
│   ├── Comun/          Resultado.cs  Pagina.cs  Filtro.cs
│   ├── Catalogos/  Ubicaciones/  Trabajadores/  Clientes/  Proveedores/
│   ├── Equipos/  Disponibilidad/  Cotizaciones/  Rentas/  Contratos/  Comercio/
│   └── Plataforma/  Empresas/                    ← migrados de las carpetas actuales
├── IServicios/
│   ├── Comun/          IAlmacenamientoArchivos.cs  IRelojSistema.cs  IFolios.cs
│   └── <un subfolder por módulo>
└── Procesos/
    └── <un subfolder por módulo — solo los que aplican>

src/Maquinaria.Infraestructura/
├── Servicios/          la implementación con EF, un subfolder por módulo
├── Persistencia/       ContextoCentral, ContextoEmpresa, configuraciones, migraciones
├── Archivos/           AlmacenamientoDisco (dev) + AlmacenamientoS3 (R2)   ← NUEVO
├── Trazabilidad/       InterceptorAuditoria                                ← NUEVO
├── Correo/  Seguridad/
└── RegistroInfraestructura.cs

src/Maquinaria.Api/
├── Controladores/      un subfolder por módulo, un *Controller.cs por recurso
├── Seguridad/          RequierePermisoAttribute.cs  PoliticasAutorizacion.cs   ← NUEVO
├── Comun/              traducción Resultado → ProblemDetails, paginación       ← NUEVO
├── Arranque/  Errores/  Salud/  TiempoDiseno/
└── Program.cs

tests/Maquinaria.Api.Tests/
└── <un subfolder por módulo> + Comun/
```

**`IServicios` y `Servicios` quedan en proyectos distintos, y es deliberado.** EF Core vive solo
en `Infraestructura` (regla dura #1 de la arquitectura); si el `Servicio` viviera junto a su
interfaz, un `Proceso` podría inyectar `ContextoEmpresa` directo y nadie lo impediría. Hoy lo
impide el compilador.

### `Controladores/` se agrupa por módulo, no por ámbito

Las dos carpetas que existen —`Plataforma/` y `Empresas/`— se pueden leer de dos formas, y hay
que fijar cuál: son **nombres de módulo**, los mismos que usa `Aplicacion`, no los dos ámbitos
del JWT. Si el eje fuera el ámbito, los once módulos de la Fase 1 caerían todos en `Empresas/`
—son todos de empresa— y quedarían treinta controladores en una carpeta.

El ámbito no se pierde por eso: ya está dicho en el `[Authorize(...)]` de la clase y en el
prefijo de la ruta. Una carpeta que repita esa información no agrega nada.

Dos reglas que se siguen de ahí:

- **El nombre de la clase es único en todo el ensamblado**, aunque el namespace lo distinga. Por
  eso el de empresa es `SesionEmpresaController` y no `SesionController`: con ruteo por
  atributos dos homónimas compilan, pero MVC deriva de las dos el mismo `ControllerName` y ahí
  `CreatedAtAction` y un `[EndpointName]` repetido se vuelven ambiguos.
- **Una clase por ruta base.** Es lo que produjo `ModulosController`, de una sola acción. La
  alternativa —un `[Route("api/plataforma")]` con la ruta completa en cada método— hace que la
  ruta deje de leerse en la clase.

### Las carpetas son técnicas; los namespaces son por módulo

Decidido el 2026-08-26 al ejecutar R0.1, y es la única cosa de la estructura que **no** sigue la
convención de .NET:

```
Aplicacion/ObjetosDTO/Rentas/RentaDto.cs        ┐
Aplicacion/IServicios/Rentas/IServicioRentas.cs ├─ tres carpetas, UN namespace:
Aplicacion/Procesos/Rentas/ProcesoCerrarRenta.cs┘  Maquinaria.Aplicacion.Rentas
```

La alternativa —un namespace por carpeta, `Aplicacion.ObjetosDTO.Rentas` y compañía— obliga a
**cada consumidor a importar dos o tres namespaces del mismo módulo**. Con 26 módulos, eso son
cabeceras de veinte `using` en cada controlador, y un controlador de rentas que necesita equipos
y clientes se lleva nueve. La carpeta contesta *qué tipo de archivo es*; el namespace contesta
*de qué habla*. Son dos preguntas y cada una la responde quien mejor puede.

Consecuencia práctica: `dotnet_style_namespace_match_folder` pasó a **`false:silent`** en
`.editorconfig`, con el porqué escrito ahí. En `true` el IDE marcaría casi todo `Aplicacion`
como sugerencia, y una sugerencia que hay que ignorar siempre entrena a ignorarlas todas.

**Y esto es lo que hizo que R0.1 fuera un movimiento puro:** ni un namespace cambió —salvo
`FormatoCodigoPlan`, que cambió de proyecto—, así que ningún `using` de ningún consumidor se
tocó y las 299 pruebas siguieron en verde por construcción, no por suerte.

### Nombres

| Cosa | Patrón | Ejemplo |
|---|---|---|
| Contrato | `IServicio<Plural>` | `IServicioEquipos` |
| Implementación | `Servicio<Plural>Ef`, `internal sealed` | `ServicioEquiposEf` |
| Proceso | `Proceso<Verbo><Sustantivo>` | `ProcesoConfirmarRenta` |
| DTO de salida | `<Entidad>Dto` | `EquipoDto` |
| DTO de entrada | verbo o sustantivo de acción | `AltaEquipo`, `CambioDeTarifa` |
| DTO de filtro | `Filtro<Plural>` | `FiltroEquipos` |
| Controlador | `<Plural>Controller` | `EquiposController` |
| Prueba | `<Clase>Pruebas` | `ProcesoConfirmarRentaPruebas` |

---

## 4. La regla de colocación

Lo que hace que todo se encuentre sin preguntar:

> **Servicio** = el que **habla con la base** (o con un tercero: correo, almacenamiento).
> **Proceso** = el que **decide**: valida, orquesta y, si hace falta, abre transacción.

```
ServicioEquipos.CrearAsync(AltaEquipo)              → habla con la base → Servicio
ServicioOcupacion.HayTraslapeAsync(equipo, rango)   → habla con la base → Servicio
ProcesoConfirmarRenta                               → decide, y compone tres → Proceso
ProcesoFinalizarOrdenCompra                         → decide, y compone tres → Proceso
```

**La regla se afinó el 2026-08-26, al aplicarla a los archivos que ya existían.** La primera
versión decía *"Proceso = compone dos o más servicios"* y contaba dependencias, y `CrearPlan`
la rompía: compone **un** solo servicio —`ICatalogoPlanes`— pero tiene seis validaciones y
decide si el plan se puede vender. Por dependencias sería Servicio; por lo que hace es
claramente un Proceso, y en `Procesos/Plataforma/` quedó.

Contar dependencias sigue siendo el buen indicio —un Proceso con una sola dependencia suele ser
un Servicio disfrazado— pero **quien manda es quién habla con la base**. Un Proceso no inyecta
`ContextoEmpresa`, y eso no depende de la disciplina de nadie: los `Procesos` viven en
`Aplicacion`, que no referencia EF Core, así que el compilador lo impide.

**`lo que aplique` se vuelve concreto:** un catálogo lleva Controlador + ObjetosDTO + IServicio
+ Servicio y **ningún** Proceso. Rentas lleva los cinco.

**El Controlador no decide nada.** Recibe, delega en un Servicio o un Proceso, y traduce el
`Resultado` a HTTP. Si un controlador tiene un `if` de negocio, está en el lugar equivocado.

### 4.1 Los tres contratos de `Comun` — escritos en R0.1

Todo Servicio y todo Proceso de la fase hablan con estos tres tipos. Están en
`Aplicacion/ObjetosDTO/Comun/`, namespace `Maquinaria.Aplicacion.Comun`.

**`Resultado` y `Resultado<T>`** — el desenlace, sin excepciones. Un rechazo de negocio es un
desenlace previsto, no un error: con excepciones, el mensaje al usuario sale de un `catch`
genérico y el manejador global tiene que distinguir *"la renta se traslapa"* de *"se cayó la
base"*. El tipo de retorno obliga a mirar `Correcto` antes de leer `Valor`.

Lo que lo hace útil es **`RazonRechazo`, y que son exactamente tres**, cada una a un código
HTTP y a ninguno más:

| Razón | HTTP | Cuándo |
|---|---|---|
| `Invalido` | **400** | dato mal capturado o que no cumple una regla de forma |
| `NoEncontrado` | **404** | la fila no existe, o está borrada lógicamente |
| `Conflicto` | **409** | choca con el estado actual o con una garantía del motor: fechas traslapadas, contrato ya autorizado, folio repetido |

Sin la razón, el controlador solo puede contestar 400 a todo —que es lo que hacen hoy los
endpoints de planes y empresas, y ahí está bien porque todos sus rechazos son captura—, y la
Fase 1 tiene los otros dos de verdad. **El 401 y el 403 no están en la lista:** los resuelve la
tubería de autorización antes de que el Proceso corra. Un Proceso que devuelve "no autorizado"
es un `[RequierePermiso]` que faltó declarar.

**`Pagina<T>`** — `Filas`, `Numero`, `Tamano` y `Total`. El `Total` es el conteo completo de lo
que cumple el filtro, no lo que trae la página: sin él la pantalla no puede decir *"51-100 de
3,842"*. Cuesta un `COUNT` extra y se paga a propósito. Un listado vacío es **200 con
`Pagina.Vacia`**, nunca 404.

**`Filtro`** — clase base con `Texto`, `Activo`, `IncluirEliminados`, `Numero`, `Tamano`,
`Orden` y `Descendente`. Cada módulo **hereda** y agrega lo suyo —`FiltroEquipos` con
`CategoriaId`, `FiltroRentas` con el rango de fechas— y no vuelve a declarar la paginación. Tres
detalles que no son cosméticos:

- **`TamanoEfectivo` acota entre 1 y 200**, y los Servicios usan eso y nunca `Tamano` crudo.
  Sin el techo, `?tamano=1000000` trae la tabla entera. Es defensa del servidor, no preferencia
  de interfaz.
- **`IncluirEliminados` exige el permiso `.eliminar`** del módulo: quien no puede borrar no
  tiene por qué ver lo borrado.
- **`Orden` se traduce contra una lista blanca** de columnas en cada Servicio, y cae al orden
  por defecto si no reconoce el valor.

---

## 5. Las rebanadas, en orden de dependencia

Cada una es `ObjetosDTO → IServicio → Servicio → (Proceso) → Controlador → Pruebas`, y se
termina antes de empezar la siguiente.

> **El detalle de codificación —qué archivos, con qué firma y en qué orden— está en
> [§10](#10-plan-de-codificación-archivo-por-archivo).** Esta sección es el alcance de cada
> rebanada; esa es cómo se escribe.

R0 va **en dos commits**, no en uno: el movimiento es mecánico y verificable en minutos; los
controladores son reescritura de verdad. Si algo se rompe, el diff que lo causó es chico.

### R0.1 — Estructura y movimiento · **HECHO el 2026-08-26**

**55 archivos movidos, todos como `R` en git** —el historial se conserva—, cero cambios de
comportamiento:

| Qué | Volumen real |
|---|---|
| `Aplicacion/{Correo,Empresas,Plataforma,Seguridad}` → `ObjetosDTO/`, `IServicios/`, `Procesos/` | 28 archivos |
| `Infraestructura/{Empresas,Plataforma}` → `Servicios/<modulo>/` | **9**, no 13: `Correo/` y `Seguridad/` no son servicios de módulo y se quedan donde están |
| `Api.Tests` plano → `Comun/`, `Empresas/`, `Plataforma/`, `Seguridad/`, `Arranque/` | 17 de 18 |
| `Aplicacion/Plataforma/FormatoCodigoPlan.cs` → `Dominio/Plataforma/` | 1, con cambio de namespace |

**Creado, porque sin esto no se puede codificar un Servicio ni un Proceso:**
`ObjetosDTO/Comun/Resultado.cs`, `Pagina.cs` y `Filtro.cs`. Ver §4.1.

**Dos correcciones que salieron al ejecutarlo:**

- **`FormatoCodigoPlan` no era ni DTO, ni interfaz, ni proceso** — es una regla de formato
  pura, sin dependencias. Su casa es `Dominio/Plataforma/`, al lado de `FormatoSlug`, que es
  exactamente lo mismo para el slug de un tenant. Tocó tres consumidores.
- **`UnitTest1.cs` sigue en la raíz de `Api.Tests`.** Es la plantilla de `dotnet new` con una
  prueba vacía; no se movió porque no pertenece a ningún módulo. **Se borra** cuando alguien lo
  confirme.

**Resultado, medido:** `Maquinaria.Api.Tests` **299 pruebas, 0 fallos**;
`Maquinaria.Dominio.Tests` 1, 0 fallos; compilación de `Maquinaria.Api` con **0 errores y 0
advertencias**. La cifra de 206 que traía la bitácora era vieja.

> **Trampa de operación, y cómo se sorteó.** Visual Studio y un `Maquinaria.Api.exe` vivo
> tenían tomadas las DLL de `src/Maquinaria.Api/bin`, así que `dotnet build` y `dotnet test`
> fallaban con `MSB3027` sin llegar a compilar nada. La salida es **compilar a otra carpeta**:
> `dotnet test tests/Maquinaria.Api.Tests -o <ruta temporal>`. No hace falta cerrar el IDE ni
> matar el proceso.

### R0.2 — Los 19 endpoints a controladores · **HECHO el 2026-08-26**

**6 archivos de endpoints borrados, 9 controladores escritos.** Los 19 endpoints (8 GET, 10
POST, 1 PATCH) quedaron así:

| Controlador | Ruta base | Acciones |
|---|---|---|
| `Plataforma/SesionController` | `api/plataforma/sesion` | 2 |
| `Plataforma/EmpresasController` | `api/plataforma/empresas` | 4 |
| `Plataforma/PlanesController` | `api/plataforma/planes` | 3 |
| `Plataforma/ModulosController` | `api/plataforma/modulos` | 1 |
| `Plataforma/SaludEsquemasController` | `api/plataforma/salud` | 1 |
| `Empresas/InvitacionesController` | `api/empresas/{slug}/invitaciones` | 2 |
| `Empresas/RestablecimientosController` | `api/empresas/{slug}/restablecimientos` | 3 |
| `Empresas/SesionEmpresaController` | `api/empresas/{slug}/sesion` | 2 |
| `Empresas/MiSesionController` | `api/mi` | 1 |

**Nueve y no seis, porque la ruta base pasó a la clase.** Con Minimal API varios recursos
colgaban del mismo `MapGroup("/api/plataforma")` y convivían en un archivo; con controladores,
dos rutas base distintas son dos clases. De ahí `ModulosController`, de una sola acción, y de
ahí que el archivo de acceso de empresa se partiera en tres — que además es mejor: cada
controlador inyecta por constructor **solo lo que usa**, en lugar de resolver los cuatro casos
de uso en cada petición.

**Dos constantes se mudaron a `Api/Seguridad/`:** `PoliticasAutorizacion` —que vivía en el
namespace global, al final de `Program.cs`— y `PoliticasLimitador`, nueva, con los tres nombres
de política del limitador que antes eran constantes dentro de los archivos de endpoints. Las
necesitan dos lados que no se conocen: `Program.cs`, que configura cupo y ventana, y el
`[EnableRateLimiting]` de cada acción.

**Verificado, no supuesto:**

| Comprobación | Resultado |
|---|---|
| `dotnet build` de `Maquinaria.Api` | **0 errores, 0 advertencias** |
| `Maquinaria.Api.Tests` | **299 pruebas, 0 fallos** — la misma cifra que antes de tocar nada |
| Rutas y `operationId` en `/openapi/v1.json` | **18 de 18 idénticos**, comparados endpoint por endpoint contra el documento del proceso viejo |
| Códigos de respuesta declarados | **2 diferencias**, las dos a favor |

Las dos diferencias son la documentación alcanzando al código: `IniciarSesionPlataforma` e
`IniciarSesionEmpresa` **ya devolvían 400** con un cuerpo incompleto y no lo declaraban. Ahora
sí. El cliente generado del frontend gana un caso que ya existía.

> **Cómo se comparó el contrato**, porque es la parte que de verdad prueba que no se rompió
> nada: se levantó el binario nuevo en `127.0.0.1:5199` con `ASPNETCORE_ENVIRONMENT=Development`
> y se bajó su `/openapi/v1.json`, contra el del proceso **viejo** que seguía corriendo en
> `:5123` con el código anterior. Un `diff` de verbo + ruta + `operationId` de las 18
> operaciones. El decimonoveno —`ReenviarInvitacionEmpresa`— solo aparece en el nuevo porque el
> proceso viejo se había levantado antes de que ese endpoint existiera.

**Lo que fija el `operationId` es `[EndpointName("...")]`**, no el `Name =` del atributo del
verbo. Se eligió explícito a propósito: si el `operationId` cambia, el cliente HTTP generado del
frontend renombra todos sus métodos y el diff es enorme.

`/salud` y `MapOpenApi` siguen mapeados a mano en `Program.cs`: solo los endpoints de negocio
son controladores. Y `app.MapControllers()` sustituyó a los seis `Mapear*()`, así que **un
controlador nuevo se descubre solo** — ya no hay una lista que haya que acordarse de ampliar.

### R1 — Transversales

Cuatro piezas, en este orden:

| # | Pieza | Dónde |
|---|---|---|
| 1 | `Resultado<T>`, `Pagina<T>`, `Filtro`, y su traducción a ProblemDetails | `ObjetosDTO/Comun/`, `Api/Comun/` |
| 2 | **`[RequierePermiso]`** — atributo + handler que lee el claim `perm` y respeta `accesoTotal` | `Api/Seguridad/` |
| 3 | **Interceptor de auditoría** — `SaveChangesInterceptor` con `correlacion_id` | `Infraestructura/Trazabilidad/` |
| 4 | **`IAlmacenamientoArchivos`** + `AlmacenamientoDisco` | `IServicios/Comun/`, `Infraestructura/Archivos/` |

**Criterio de aceptación:**

- un endpoint con `[RequierePermiso("planes.crear")]` responde **403** a un token sin ese
  permiso y **200** con él, y un token con `accesoTotal` lo salta;
- un `INSERT`, un `UPDATE` y un `DELETE` dejan fila en `auditoria` con `usuario_id`, `roles`,
  `ip`, `origen` y `correlacion_id`, y **`hash_contrasena` y los `hash_token` NO aparecen en el
  `jsonb`**;
- los contextos que construye `ProveedorContextoEmpresa` **siguen sin interceptores**, a
  propósito: el aprovisionamiento y `migrar-empresas` no auditan fila por fila;
- un archivo se sube, se recupera por URL firmada de vigencia corta y la ruta va prefijada por
  tenant.

#### Cómo se implementa `[RequierePermiso]` — resuelto el 2026-08-26, para construir en R1

**Sí lleva policy, pero registrada en un bucle, no 132 a mano ni un provider dinámico.** Tres
archivos en `Api/Seguridad/` más un bucle en `Program.cs`:

| Archivo | Qué es |
|---|---|
| `RequisitoPermiso.cs` | `IAuthorizationRequirement` que solo carga la clave |
| `ManejadorPermiso.cs` | `AuthorizationHandler<RequisitoPermiso>`: si hay `acceso_total` concede; si no, parte el claim `perm` por espacios y busca la clave |
| `RequierePermisoAttribute.cs` | `AuthorizeAttribute` cuyo constructor asigna `Policy = clave` |

```csharp
foreach (var modulo in ClavesModulo.Todas)
foreach (var accion in AccionesPermiso.Todas)
{
    var clave = $"{modulo}.{accion}";
    autorizacion.AddPolicy(clave, p => p
        .RequireAuthenticatedUser()
        .RequireClaim(ProveedorTokensJwt.ClaimAmbito, ProveedorTokensJwt.AmbitoEmpresa)
        .AddRequirements(new RequisitoPermiso(clave)));
}
```

Cuatro decisiones, con su razón:

- **Policy y no un `IAuthorizationFilter` suelto**, porque la tubería de autorización distingue
  sola el 401 —sin token— del 403 —con token y sin permiso—, y un filtro propio casi siempre
  acaba devolviendo 403 a quien ni venía autenticado.
- **Requisito con handler y no `RequireClaim`**, porque los permisos viajan en **un solo claim
  `perm` separado por espacios** y `RequireClaim` compara el valor completo, no una palabra
  dentro de él.
- **El ámbito `empresa` va DENTRO de la policy del permiso**, así que un token de plataforma no
  puede satisfacerla aunque alguien olvide el `[Authorize(Empresa)]` del controlador.
- **Bucle y no `IAuthorizationPolicyProvider` dinámico.** Con el bucle, una clave mal escrita
  revienta al llegar la petición —"policy not found"—; con el provider dinámico devuelve **403
  para siempre, en silencio**, y un endpoint que exige un permiso inexistente es un endpoint
  inalcanzable. Eso tiene que doler al primer intento.

Y **el handler no llama `Fail()`**: no conceder ya niega, mientras que `Fail()` es definitivo y
cerraría la puerta a que otro handler conceda el día que haya permisos por ubicación o por
cliente.

**Dos pruebas cierran la pieza**, y la segunda es la que importa:

1. cada clave que exige un `[RequierePermiso]` en el ensamblado existe en
   `ClavesModulo.Todas` × `AccionesPermiso.Todas` — por reflexión;
2. **cada endpoint de empresa exige algún permiso**, con una lista explícita de excepciones
   —login, refresco, invitaciones, restablecimiento— declarada en la propia prueba. Es lo que
   impide que la fase crezca a 50 endpoints con uno abierto por olvido.

#### Estado de R1 al 2026-08-26 · dos de cuatro piezas

| Pieza | Estado |
|---|---|
| `Resultado`, `Pagina`, `Filtro` + **`Api/Comun/ResultadosHttp.cs`** | **hecho** |
| **`[RequierePermiso]`** — `RequisitoPermiso`, `ManejadorPermiso`, el atributo, el bucle de policies y el handler en DI | **hecho** |
| Interceptor de auditoría | pendiente |
| `IAlmacenamientoArchivos` + `AlmacenamientoDisco` | pendiente — bloquea R6 |

Dos archivos que no estaban en el inventario y salieron necesarios:

- **`Aplicacion/ObjetosDTO/Comun/ClavesPermiso.cs`** — el producto de `ClavesModulo` ×
  `AccionesPermiso`, ya compuesto. El bucle de policies lo necesita, y **`Api` no debe mirar a
  `Dominio`**: la regla de capas dice que habla con `Aplicacion`. Aquí se multiplica una vez y
  se expone armado.
- **`Infraestructura/Servicios/Comun/ErroresPostgres.cs`** — los `SqlState` que son regla de
  negocio y no fallo: `23505` único, **`23P01` traslape**, `23514` check, `23503` foránea, más
  la extensión que los lee de una `DbUpdateException`. Se escribió aquí porque los catálogos ya
  necesitan traducir el `UNIQUE`, y **R7 lo extiende para el `EXCLUDE`** en lugar de inventarlo
  de nuevo.

**Verificado:** un endpoint con `[RequierePermiso]` y sin token responde **401**; las 39
pruebas nuevas del manejador, del traductor y del filtro pasan. El 403 con token sin permiso lo
cubren las pruebas del manejador, que es donde vive la decisión.

### R2 — Catálogos · 7 tablas

`categoria_equipo` · `tipo_equipo` · `marca` · `modelo_equipo` · `tarifa` · `clausula` · `puesto`

CRUD puro, sin Procesos. **Es la rebanada que fija el patrón que copian las nueve siguientes**,
así que su revisión es la más importante de la fase.

- **Controladores:** 7, uno por recurso.
- **Reglas:** `modelo_equipo` cuelga de `marca` y de `tipo_equipo`; `tarifa` lleva `unidad`
  (Hora, Dia, Semana, Mes, Evento, Kilometro) y las banderas `AplicaRenta` / `AplicaVenta`.
  **El precio NO vive en `tarifa`** — vive en `equipo_tarifa` (R6).
- **Criterio:** los 7 recursos con listado paginado y filtrable, alta, edición, retiro y
  permisos verificados.

#### Estado al 2026-08-26 · dos de siete, y la decisión de no abstraer

`CategoriaEquipo` y `Marca` están escritos de punta a punta —DTO, interfaz, servicio,
controlador, DI— con sus 10 endpoints en `/openapi/v1.json`. Faltan `TipoEquipo`,
`ModeloEquipo`, `Tarifa`, `Clausula` y `Puesto`.

**Un hallazgo que corrige el modelo de esta rebanada: los catálogos NO tienen `eliminado_en`.**
Solo lo tienen `equipo`, `archivo` y `tenant` — verificado sobre las 78 clases del dominio, y
`guias/convenciones.md` lo da por hecho para «entidades de negocio» sin que sea cierto. En
consecuencia:

- **no hay `DELETE`, hay `PATCH .../activo`.** Un borrado lógico no cabe en el modelo y el
  físico lo impide la FK de `tipo_equipo` / `modelo_equipo` en cuanto el catálogo se use una
  vez. Es el mismo patrón que `Plan.Activo` del catálogo comercial, ya decidido y por la misma
  razón: el negocio no quiere borrar el registro, quiere dejar de ofrecerlo;
- el quinto método de la interfaz es **`CambiarActivoAsync`**, no `EliminarAsync`;
- **desactivar un catálogo con hijos se permite**, a propósito: no rompe nada y es lo que se
  hace al retirar una línea de negocio;
- `Filtro.IncluirEliminados` **no aplica** a estos servicios. Solo lo honran los de las tres
  entidades que sí tienen la columna. Ver §7 #4, corregido.

**Y la decisión que el plan dejaba abierta: no se extrae una base genérica de catálogo.** Con
los dos escritos, la evidencia es que **se parecen en la forma de sus operaciones y no comparten
una línea de contenido**: `categoria_equipo` tiene código único, nombre y descripción;
`marca` tiene el nombre como clave única y nada más; `modelo_equipo` cuelga de dos FK. Lo que
una base ahorraría es la mecánica de paginar y el `try/catch` del `UNIQUE`; lo que cambia es
todo lo demás. Se revisará otra vez al terminar los siete.

**Un defecto del esquema que salió al escribir `Marca`:** el `UNIQUE` de `marca.nombre` es
sensible a mayúsculas, así que para el motor `'Caterpillar'` y `'CATERPILLAR'` son dos marcas.
El servicio lo cubre comprobando con `ILIKE` exacto antes de insertar, pero **eso no aguanta
concurrencia**: dos altas simultáneas con distinta capitalización pasan las dos. El arreglo de
verdad es un índice único sobre `lower(nombre)`, y va en la migración de §6.

> **R2 CERRADO el 2026-08-26.** Los siete catálogos escritos de punta a punta —DTO, interfaz,
> servicio, controlador, DI—, **35 endpoints**. `TipoEquipo` y `ModeloEquipo` llevan filtro
> propio (`FiltroTiposEquipo`, `FiltroModelosEquipo`) porque «los de esta categoría» y «los de
> esta marca» son la consulta frecuente; `Tarifa` lleva `FiltroTarifas` para ofrecer solo las
> de renta al cotizar. Los permisos por catálogo: `equipos.*` para categoría, tipo, marca y
> modelo; `rentas.*` para tarifa; `contratos.*` para cláusula; `usuarios.*` para puesto.

### R3 — Ubicaciones · 1 tabla

`ubicacion`, con sus tres tipos: Bodega (1), Sucursal (2), Patio (3).

- **`almacena_equipo` y `es_administrativa` son columnas GENERADAS** por Postgres: el DTO de
  entrada **no las acepta** y el de salida las expone como solo lectura. Capturarlas permitiría
  crear una "bodega que cotiza", que no existe.
- **`Ubicacion.Telefono` se valida con `FormatoTelefono`** (`Dominio/Comun/`). La columna no
  tiene `CHECK`.
- **Criterio:** intentar escribir las dos banderas devuelve 400 antes de tocar la base.

### R4 — Trabajadores · 1 tabla

`trabajador`, con su `puesto` y su `ubicacion`. Depende de R2 y R3.

> Un trabajador es una **persona**; un usuario es una **cuenta**. El operador de patio puede no
> tener acceso al sistema y hay que poder registrarlo igual. No se cruzan en esta fase.

- **`Trabajador.Telefono` se valida con `FormatoTelefono`.**

### R5 — Clientes y Proveedores · 2 tablas

`cliente` —con su contacto y su domicilio **dentro**— y `proveedor`.

- **Las tres validaciones de formato salen de `Dominio/Comun/`, no se reescriben:** `FormatoRfc`
  para `Cliente.Rfc` y `Proveedor.Rfc`, `FormatoTelefono` para los teléfonos y `FormatoCorreo`
  para los correos. **Ninguna de esas columnas tiene `CHECK` en la base**, así que lo que no se
  rechace aquí se guarda tal cual y no se vuelve a detectar nunca.
- **Criterio:** RFC único cuando viene y con formato válido —12 o 13 caracteres, `Ñ` y `&`
  aceptados—; un teléfono con separadores se rechaza con mensaje; búsqueda por nombre con
  `pg_trgm`.

> **R3, R4 y R5 CERRADOS el 2026-08-26**, 20 endpoints entre los cuatro recursos. Tres
> decisiones que salieron al escribirlos, todas por adaptarse al esquema **ya migrado**:
>
> - **`AlmacenaEquipo` y `EsAdministrativa` se filtran por `Tipo`, no por la propiedad.** Son
>   propiedades calculadas de C# sin setter, así que EF no las traduce a SQL; el predicado
>   equivalente es `Tipo IN (1,3)`, que es exactamente lo que contienen las columnas generadas.
> - **Bajar un patio a sucursal se rechaza si tiene equipos.** El trigger
>   `equipo_exigir_almacen` solo corre al insertar o mover un equipo, no al cambiar el tipo de
>   la ubicación, así que el cambio dejaría filas inválidas que nada volvería a mirar. Es el
>   único lugar donde se puede impedir.
> - **`trabajador` y `cliente` se retiran con `PATCH .../estado`, no con activo.** El CHECK
>   `trabajador_baja_coherente` exige que el estado Baja y la fecha de baja existan a la vez;
>   moverlos en dos llamadas dejaría un instante con la fila inválida, así que viajan juntos en
>   un solo cuerpo. `Filtro.Activo` se interpreta como «no dado de baja», que es lo que espera
>   quien marca la casilla.
>
> Y los tres validadores de `Dominio/Comun` quedaron cableados donde el esquema no protege
> nada: `FormatoRfc` en cliente y proveedor, `FormatoTelefono` en ubicación, trabajador,
> cliente y proveedor, `FormatoCorreo` en trabajador, cliente y proveedor.

### R6 — Equipos · 3 tablas

`equipo` · `equipo_archivo` · `equipo_tarifa`

La rebanada más grande de catálogo. Depende de R1 (archivos), R2 y R3.

- **Procesos:** ninguno todavía.
- **Reglas que ya viven en el motor y hay que traducir a mensajes legibles:**
  - un equipo solo puede estar en una ubicación que **almacene** — trigger;
  - **un solo precio vigente** por concepto, equipo y cliente — `EXCLUDE`;
  - `equipo` **no tiene `proveedor_id`**: el proveedor se alcanza por
    `equipo → orden_compra_detalle → orden_compra → proveedor` (R11).
- **Criterio:** expediente completo del equipo —datos, documentos por tipo (Foto, Factura,
  Poliza, Manual, Certificado, Otro) y precios con vigencia—, y el `EXCLUDE` de precio devuelve
  un 409 con mensaje de negocio, no un `23P01` crudo.

### R7 — Disponibilidad y traspasos · 2 tablas

`ocupacion_equipo` · `transferencia_equipo` — **el corazón técnico del entregable.**

- **`IServicioOcupacion`** es el único que escribe en `ocupacion_equipo`. Nadie más.
- **La consulta de disponibilidad es UNA consulta** con índice GiST, no cinco joins.
- **Traducción del `23P01`:** el `EXCLUDE` `ocupacion_sin_traslape` es la garantía real; el
  Servicio la convierte en un 409 que dice **qué equipo y qué periodo** choca. Nunca se pregunta
  "¿existe?" y luego se inserta: bajo concurrencia las dos transacciones leerían "no existe" y
  las dos insertarían.
- **Traspasos:** solo de una ubicación que almacene a otra que almacene — trigger. Un traspaso
  ocupa el calendario con `motivo = Traslado`.
- **Criterio:** las 30 pruebas de garantías que ya existen contra la base real siguen en verde,
  y ahora hay un endpoint que responde "¿qué equipos hay libres del 10 al 20 de septiembre?".

### R8 — Cotizaciones · 2 tablas

`cotizacion` · `cotizacion_linea`

- **Máquina de estados:** Borrador (1) → Enviada (2) → EnRevision (3) → Aceptada (4) /
  Rechazada (5) / Vencida (6) / Cancelada (7).
- **Una cotización NO reserva nada:** no escribe en `ocupacion_equipo`. Por eso sus líneas van
  en **una** tabla y pueden referenciar un **tipo** de equipo en lugar de un equipo concreto
  —"una excavadora de 20 t" antes de saber cuál—, o **ninguno de los dos**, que es el caso del
  flete.
- **Regla del motor:** una cotización solo sale de una ubicación **administrativa**.
- **La fase no calcula precios.** Multiplica cantidad por precio unitario y suma líneas. No
  escoge la tarifa conveniente, no decide si 12 días son semana + días, no prorratea.
- **Criterio:** el importe aplicado queda **congelado en la línea**. Si mañana se automatiza el
  cálculo, las cotizaciones viejas siguen mostrando lo que se cobró.

### R9 — Rentas · 4 tablas

`renta` · `renta_linea` · `renta_concepto` · `extension_renta` — **el criterio de salida.**

- **Procesos:** `ProcesoConfirmarRenta`, `ProcesoExtenderRenta`, `ProcesoCerrarRenta`,
  `ProcesoCancelarRenta`, `ProcesoRentaDesdeCotizacion`.
- **`renta_linea` es lo que se renta** —una fila por equipo, `equipo_id NOT NULL`— y es **lo
  único que genera filas de `ocupacion_equipo`**. `renta_concepto` es lo que se cobra además
  —flete, operador, maniobras— y no lleva equipo.
- **El operador es un `trabajador`** en un `renta_concepto`: solo quién va y cuánto se cobra.
  Sin jornadas ni horas extra.
- **`lugar_descripcion` es obligatoria.** No hay tabla `obra`.
- **Criterio de salida de la fase, completo:** cotizar → aprobar → rentar → cerrar funciona, y
  **es imposible rentar dos veces el mismo equipo en fechas traslapadas** — probado con dos
  transacciones simultáneas, no con dos llamadas secuenciales.

### R10 — Contratos · 2 tablas

`contrato` · `contrato_clausula`

- **La cadena es `cotizacion → renta → contrato`**, no la de la especificación.
  `contrato.renta_id` es `NOT NULL` y **único**: un contrato por renta.
- **El texto de la cláusula se congela al generar el contrato.** `contrato_clausula` copia
  título y texto; `clausula_id` queda solo como referencia de dónde salió, y es **nullable**
  porque una cláusula puede ser propia, negociada con ese cliente y ausente del catálogo.
- **Una vez fuera de Borrador, el contrato no se toca** — ni él ni sus cláusulas. Lo impone un
  trigger, no la disciplina del programador.
- **Criterio:** editar un contrato autorizado devuelve 409; el texto de un contrato ya generado
  no cambia al corregir la plantilla del catálogo.

### R11 — Compra y venta · 4 tablas

`orden_compra` · `orden_compra_detalle` · `orden_venta` · `orden_venta_detalle`

- **Mismo flujo, simétrico:** Borrador (1) → Autorizada (2) → Finalizada (3) / Cancelada (4).
- **Procesos:** `ProcesoFinalizarOrdenCompra` —registra el equipo en el catálogo y lo pone a
  disposición— y `ProcesoFinalizarOrdenVenta` —lo saca del parque y **cierra su calendario de
  ocupación** para que no pueda rentarse después—.
- **Criterio:** un equipo vendido no aparece como disponible en ninguna consulta de R7.

### R12 — Cierre de fase

- `Dockerfile` de Railway;
- **`Jwt__Llave` en Railway, con un valor DISTINTO al de `user-secrets`.** Doble guion bajo, que
  es el separador de sección en variables de entorno. Distinto porque si la llave de desarrollo
  se filtra —una laptop, una captura de pantalla, un pegado en un chat— producción no se ve
  tocada. Con la validación al arranque (§1.2), una variable mal puesta ya no deja levantar el
  contenedor, que es el comportamiento que se quiere ahí;
- **`migrar-empresas` corrido otra vez contra Neon**, para aplicar la migración de §6 a las tres
  bases. El comando ya se ejercitó de verdad el 2026-08-25 —esa corrida fue la que destapó sus
  defectos—, así que aquí no se está estrenando la herramienta: se está cerrando el desfase que
  deja la migración nueva. Escribir la migración no migra ninguna base;
- `GET /api/plataforma/salud/esquemas` reportando todas las bases al día, con la versión
  disponible del binario que responde;
- volver a correr `docs/auditar-diseno-vs-base.py`: el DDL de diseño contra
  `information_schema`, cero desajustes;
- recorrido completo del criterio de salida contra una empresa real.

---

## 5.bis Avance al 2026-08-26 · **las once rebanadas escritas**

| Rebanada | Estado | Endpoints |
|---|---|---|
| R0.1 · estructura | **hecha** | — |
| R0.2 · controladores | **hecha** | 19 |
| R1 · transversales | **3 de 4** — falta el interceptor de auditoría | — |
| R2 · catálogos (7) | **hecha** | 35 |
| R3 · ubicaciones | **hecha** | 5 |
| R4 · trabajadores | **hecha** | 5 |
| R5 · clientes y proveedores | **hecha** | 10 |
| R6 · equipos, precios y documentos | **hecha** | 13 |
| R7 · disponibilidad y traspasos | **hecha** | 6 |
| R8 · cotizaciones | **hecha** | 7 |
| R9 · rentas + 5 procesos | **hecha** | 15 |
| R10 · contratos | **hecha** | 7 |
| R11 · compra y venta | **hecha** | 14 |

**136 endpoints en `/openapi/v1.json`**, 338 pruebas en verde, compilación con 0 errores y 0
advertencias. Todo endpoint de empresa exige su permiso, comprobado por reflexión en cada
corrida.

### Las piezas transversales que salieron por el camino

Ninguna estaba en el inventario de §10.3 y las once rebanadas las necesitaban:

| Pieza | Por qué |
|---|---|
| **`IUnidadDeTrabajo` / `UnidadDeTrabajoEf`** | un Proceso vive en `Aplicacion`, que no referencia EF, así que no puede abrir una transacción. Sin esta abstracción las salidas eran mover los Procesos a `Infraestructura` —y perder la frontera— o dejar que cada Servicio confirmara por su cuenta, que es lo que hace imposible deshacer una renta a medio confirmar |
| **`IFolios` / `ServicioFoliosEf`** | los cinco documentos tienen `folio` único y nada lo generaba. Sin secuencias en el esquema migrado, se calcula leyendo el máximo del año — con la limitación de concurrencia documentada y reintento ante el `23505` |
| **`ErroresPostgres`** | los `SqlState` que son regla de negocio: `23505`, **`23P01`**, `23514`, `23503` y **`P0001`** —el `RAISE EXCEPTION` de los triggers—. Es lo que convierte las garantías del motor en 409 con mensaje en lugar de 500 |
| **`IAlmacenamientoArchivos` / `AlmacenamientoDisco`** | bloqueaba R6. El prefijo del tenant lo pone la implementación, nunca el llamador: es lo único que separa los archivos de un cliente de los de otro dentro de un bucket compartido |

### Lo que quedó pendiente, dicho con precisión

1. **El interceptor de auditoría.** Las dos tablas `auditoria` siguen vacías: nada de lo que
   hacen estos 136 endpoints queda auditado. Es la última pieza de R1.
2. **Las pruebas contra Postgres real.** Las 338 son unitarias y ninguna toca la base. Lo que
   falta probar es justo lo que el motor garantiza —el `EXCLUDE` de no-traslape bajo dos
   transacciones simultáneas, el trigger del contrato inmutable, los `UNIQUE`—, y eso ningún
   doble lo reproduce. **Sin esa corrida, el criterio de salida está escrito pero no
   demostrado.**
3. **R12**, el cierre: `Dockerfile`, `Jwt__Llave` en Railway, `migrar-empresas` y el recorrido
   completo contra una empresa real.

### Las cuatro adaptaciones al esquema migrado

Decidido el 2026-08-26: **el esquema no se toca en esta fase.** Donde el modelo migrado no
alcanza, el código lo rodea:

| Hueco | Cómo se rodeó |
|---|---|
| `MotivoOcupacion` sin `Venta` | `ProcesoFinalizarOrdenVenta` cierra el calendario con **`Bloqueo` sin fecha de fin** y una nota que dice de qué venta salió. El efecto sobre la disponibilidad es idéntico; lo que se pierde es distinguir «vendido» de «bloqueado» leyendo solo el motivo |
| `EstadoContrato` sin `Cancelado` | el contrato va Borrador → Autorizado → Firmado → Terminado. **No hay cancelación**, así que el camino que el alcance describe —cancelar y hacer uno nuevo— no está disponible |
| Folios sin secuencia | máximo del año + 1, con reintento ante el `23505`. Con veinte capturistas simultáneos la colisión deja de ser rara |
| `marca.nombre` sensible a mayúsculas | comprobación previa con `ILIKE` exacto. No aguanta concurrencia: dos altas simultáneas con distinta capitalización pasan las dos |
| `orden_compra_detalle` con un solo `equipo_id` | **una línea, una máquina**: `cantidad` distinta de 1 se rechaza con mensaje. Tres excavadoras iguales son tres líneas — que además es lo correcto, porque cada una tiene su número de serie |

**El esquema no se toca.** Decidido el 2026-08-26: la implementación se adapta a las 28 tablas
tal como están migradas, así que los cuatro huecos de §6 **no** se cierran con una migración en
esta fase y lo que toca es rodearlos:

| Hueco | Cómo se rodea |
|---|---|
| `MotivoOcupacion` sin `Venta` | al finalizar una venta, el calendario se cierra con `Bloqueo` y nota, no con un motivo nuevo |
| `EstadoContrato` sin `Cancelado` | el contrato va Borrador → Autorizado → Firmado → Terminado. No hay cancelación |
| Folios sin secuencia | se calculan por consulta dentro de la transacción y se reintenta ante el `23505`; el `UNIQUE` sigue siendo la garantía |
| `marca.nombre` sensible a mayúsculas | comprobación previa con `ILIKE` exacto, con la limitación de concurrencia anotada |

---

## 6. Cuatro huecos del modelo, y por qué NO se migran en esta fase

**El esquema NO está tan completo como dicen los documentos.** Encontrados el 2026-08-26
leyendo los enums contra los `CHECK`:

### 6.1 `MotivoOcupacion` no tiene `Venta`

[`06-alcance-fase1.md`](06-alcance-fase1.md) §5 dice que `motivo = Venta` es lo que conecta la
venta de equipo con la garantía de no-traslape. El enum tiene seis valores —Renta, Reserva,
Mantenimiento, Reparacion, Traslado, Bloqueo— y el `CHECK` dice `motivo BETWEEN 1 AND 6`.

**Sin `Venta = 7`, `ProcesoFinalizarOrdenVenta` (R11) no puede cerrar el calendario del equipo
como está diseñado.** Requiere migración: valor nuevo en el enum y `CHECK` a `1 AND 7`.

### 6.2 `EstadoContrato` no tiene `Cancelado`

El alcance dice `Borrador → Autorizado → Terminado (+ Cancelado)` y que *"cambiar un contrato
autorizado exige cancelarlo y hacer uno nuevo"*. El enum es Borrador, Autorizado, **Firmado**,
Terminado, con `CHECK estado BETWEEN 1 AND 4`.

Dos desajustes: **no existe `Cancelado`**, así que el camino que el alcance describe es
imposible; y existe `Firmado`, que el alcance no menciona. Requiere decidir los estados de
verdad y migrar.

### 6.3 El `UNIQUE` de `marca.nombre` distingue mayúsculas

Salió al escribir `ServicioMarcasEf` el 2026-08-26. El índice es sobre `nombre` tal cual, así
que para el motor `'Caterpillar'` y `'CATERPILLAR'` son dos marcas distintas y las acepta las
dos. El servicio lo cubre comprobando con `ILIKE` exacto antes de insertar, y **eso no aguanta
concurrencia**: dos altas simultáneas con distinta capitalización pasan la comprobación y las
dos insertan.

El arreglo es un índice único sobre `lower(nombre)`. Conviene revisar de paso los demás
`UNIQUE` de texto capturado a mano —`categoria_equipo.codigo`, `tipo_equipo`, `tarifa.codigo`—
antes de escribir la migración: el mismo defecto puede estar repetido.

### 6.4 Los folios no tienen generador

`cotizacion`, `renta`, `contrato`, `orden_compra` y `orden_venta` tienen `folio` **`required` y
con índice `UNIQUE`**, y **nada lo genera**. Dos usuarios capturando al mismo tiempo chocan con
un `23505`.

**Recomendación:** una **secuencia de Postgres por tipo de documento** en cada base de empresa
—el aislamiento es físico, así que no hay colisión entre empresas— más el prefijo y el formato
en `parametro` (`COT-2026-00001`). Un contador en tabla propia exigiría bloqueo explícito; una
secuencia no. Requiere migración.

> Los tres se resuelven en **una sola migración de `ContextoEmpresa`**, antes de R7, y esa
> migración **termina con una corrida de `migrar-empresas`** — escribir la migración no migra
> ninguna base.

---

## 7. Decisiones que esta fase tiene que cerrar

| # | Decisión | Recomendación |
|---|---|---|
| 1 | **`EstadoRenta` tiene 10 valores** y la fase no hace logística | usar 6: Borrador → Confirmada → Activa → Devuelta → Cerrada (+ Cancelada). `PorEntregar` y `EnTraslado` quedan para M8 en Fase 2; `PorVencer` y `Vencida` se **derivan de la fecha**, no se capturan |
| 2 | **Folios** | secuencia de Postgres + prefijo en `parametro` (§6.3) |
| 3 | **Paginación y filtros** | un contrato único: `Pagina<T>` con `pagina`, `tamano`, `total`, y `Filtro` con `texto`, `activo`, orden. Se define en R1 y no se vuelve a discutir |
| 4 | **Borrado lógico** | **Corregido el 2026-08-26.** Solo `equipo`, `archivo` y `tenant` tienen `eliminado_en`; los catálogos, `ubicacion`, `trabajador`, `cliente`, `renta`, `cotizacion` y `contrato` **no**. Así que: donde existe la columna, `eliminado_en` nunca sale en el DTO, el listado la filtra siempre y `?incluirEliminados=true` exige el permiso `.eliminar`; **donde no existe, retirar es `PATCH .../activo`** y no hay `DELETE`. La opción de agregar `eliminado_en` a las demás queda abierta y **no** se toma en esta fase |
| 5 | **Dónde valida** | tres niveles, y no se mezclan: **forma trivial** —obligatorio, largo, rango— en el DTO con DataAnnotations, que `[ApiController]` convierte en 400 automático; **formatos compartidos** —RFC, teléfono, correo— con los validadores de `Dominio/Comun/`, nunca con una regex nueva en el DTO; **reglas de negocio** en el Servicio o el Proceso, con `Resultado.Rechazado(motivo)`. Un formato duplicado en un DTO se desincroniza del de plataforma y entonces el mismo RFC es válido en un alta y no en otra |
| 6 | **Refresh token en cookie `HttpOnly`** | **queda fuera de esta fase.** Hoy viaja en el cuerpo JSON; el cambio solo tiene sentido con el navegador del otro lado, y esta fase no tiene frontend |

---

## 8. Fuera de alcance, dicho para que nadie lo busque

Logística completa (M8), las dos inspecciones (M9, M10), evidencias y firmas (M11), horómetros
(M12), todo el taller (M13–M18), pagos y cobranza (M19), facturación (M20), notificaciones
(M26), reportes (M27), QR (M29), subrentas (M30) y el dashboard (M1).

Y del backend en concreto: **el cálculo de precios**. La fase captura importes. Siguen abiertos
los depósitos y el combustible.

---

## 9. Definición de terminado, por rebanada

Una rebanada no está lista hasta que las seis se cumplen:

1. compila sin advertencias nuevas;
2. sus endpoints están en `/openapi/v1.json` con `operationId` explícito y respuestas
   declaradas;
3. **cada endpoint exige su permiso** con `[RequierePermiso]`;
4. tiene pruebas del camino feliz **y** del rechazo de cada regla de negocio;
5. las reglas que garantiza el motor están probadas **contra la base real**, no con un doble;
6. lo que se decidió y por qué está en `guias/estado-y-pendientes.md`, con fecha.

---

## 10. Plan de codificación, archivo por archivo

Las rebanadas de §5 dicen **qué** entra y con qué criterio se cierra. Esta sección dice **qué
archivos se escriben, con qué firma y en qué orden**. Escrita el 2026-08-26, después de R0.

### 10.1 Orden de escritura dentro de una rebanada

Siempre el mismo, y no es arbitrario: cada paso compila sin que exista el siguiente.

```
1. ObjetosDTO       ← no dependen de nada. Compilan solos.
2. IServicios       ← dependen de los DTO
3. Servicios (Ef)   ← implementan la interfaz. Aquí se escriben las consultas.
4. Procesos         ← solo si la rebanada los tiene
5. Controlador      ← lo último de producción: no puede existir sin lo que delega
6. Pruebas          ← del Servicio y de cada Proceso
7. Registro en DI   ← una línea por tipo en RegistroInfraestructura
```

El paso 7 se olvida y el síntoma es feo: compila, arranca, y revienta en la primera petición
con *"Unable to resolve service"*. **Va en el mismo commit que el controlador.**

### 10.2 El patrón canónico

`CategoriaEquipo` es la primera cosa que se escribe de la Fase 1, y **las nueve rebanadas
siguientes copian esta forma**. Cinco archivos:

```
Aplicacion/ObjetosDTO/Catalogos/CategoriaEquipoDtos.cs
Aplicacion/IServicios/Catalogos/IServicioCategoriasEquipo.cs
Infraestructura/Servicios/Catalogos/ServicioCategoriasEquipoEf.cs
Api/Controladores/Catalogos/CategoriasEquipoController.cs
Api.Tests/Catalogos/ServicioCategoriasEquipoPruebas.cs
```

**Los DTO, dos records en un archivo.** Un archivo por entidad y no uno por record: es el mismo
criterio de `CatalogoComercial.cs`, que ya agrupa cuatro.

```csharp
namespace Maquinaria.Aplicacion.Catalogos;

public sealed record CategoriaEquipoDto(
    Guid Id, string Codigo, string Nombre, string? Descripcion, bool Activo);

public readonly record struct AltaCategoriaEquipo(
    string Codigo, string Nombre, string? Descripcion);
```

**No hay `FiltroCategoriasEquipo`.** Un catálogo se filtra con el `Filtro` base —texto, activo,
paginación, orden— y nada más. El primer módulo que necesita campos propios es Equipos, con
`FiltroEquipos : Filtro`.

**El contrato del servicio.** Cinco métodos, y son los mismos para los siete catálogos:

```csharp
public interface IServicioCategoriasEquipo
{
    Task<Pagina<CategoriaEquipoDto>> ListarAsync(Filtro filtro, CancellationToken ct);

    Task<CategoriaEquipoDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<CategoriaEquipoDto>> CrearAsync(AltaCategoriaEquipo alta, CancellationToken ct);

    Task<Resultado<CategoriaEquipoDto>> EditarAsync(
        Guid id, AltaCategoriaEquipo cambio, CancellationToken ct);

    // CambiarActivoAsync y NO EliminarAsync: los catalogos no tienen eliminado_en, asi que
    // retirar es desactivar. Ver R2, "Estado al 2026-08-26".
    Task<Resultado<CategoriaEquipoDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct);
}
```

Dos asimetrías deliberadas:

- **`ObtenerAsync` devuelve `T?` y no `Resultado<T>`.** "No existe" es el único desenlace que no
  es el feliz, y el controlador ya sabe traducir un nulo a 404. Envolverlo costaría una capa
  para no decir nada más.
- **`ListarAsync` no devuelve `Resultado`.** Un listado no se rechaza: un filtro sin
  coincidencias es una `Pagina.Vacia`, que es 200. Si el filtro viene absurdo, `TamanoEfectivo`
  lo acota.

**El servicio, y las cuatro cosas que hace siempre.** Es donde vive el SQL, así que es el
archivo que hay que revisar con cuidado:

```csharp
internal sealed class ServicioCategoriasEquipoEf(ContextoEmpresa bd)
    : IServicioCategoriasEquipo
{
    public async Task<Pagina<CategoriaEquipoDto>> ListarAsync(Filtro filtro, CancellationToken ct)
    {
        var consulta = bd.CategoriasEquipo.AsNoTracking();

        // 1. El borrado logico se esconde SIEMPRE, salvo que se pida a proposito.
        if (!filtro.IncluirEliminados)
        {
            consulta = consulta.Where(c => c.EliminadoEn == null);
        }

        // 2. Sobre que columnas aplica el texto lo decide cada servicio.
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var t = filtro.Texto.Trim();
            consulta = consulta.Where(c => EF.Functions.ILike(c.Nombre, $"%{t}%")
                                        || EF.Functions.ILike(c.Codigo, $"%{t}%"));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(c => c.Activo == activo);
        }

        // 3. El COUNT va ANTES del Skip/Take, sobre la consulta ya filtrada.
        var total = await consulta.LongCountAsync(ct);

        // 4. El orden sale de una LISTA BLANCA. Nunca se interpola el nombre de columna.
        consulta = (filtro.Orden, filtro.Descendente) switch
        {
            ("codigo", false) => consulta.OrderBy(c => c.Codigo),
            ("codigo", true)  => consulta.OrderByDescending(c => c.Codigo),
            (_, true)         => consulta.OrderByDescending(c => c.Nombre),
            _                 => consulta.OrderBy(c => c.Nombre),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(c => new CategoriaEquipoDto(
                c.Id, c.Codigo, c.Nombre, c.Descripcion, c.Activo))
            .ToListAsync(ct);

        return new Pagina<CategoriaEquipoDto>(
            filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }
}
```

**La proyección va en el `Select` y nunca se materializa la entidad.** Traer
`CategoriaEquipo` completa y mapearla en memoria arrastra columnas que nadie usa y, en
`equipo`, las relaciones enteras. Es la diferencia entre un `SELECT` de cinco columnas y uno de
treinta.

**El controlador, que no decide nada:**

```csharp
[ApiController]
[Route("api/catalogos/categorias-equipo")]
[Tags("Catalogos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class CategoriasEquipoController(IServicioCategoriasEquipo servicio)
    : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarCategoriasEquipo")]
    [ProducesResponseType<Pagina<CategoriaEquipoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync([FromQuery] Filtro filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpPost]
    [RequierePermiso("equipos.crear")]
    [EndpointName("CrearCategoriaEquipo")]
    [ProducesResponseType<CategoriaEquipoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAsync(AltaCategoriaEquipo alta, CancellationToken ct)
        => this.AHttp(
            await servicio.CrearAsync(alta, ct),
            r => $"/api/catalogos/categorias-equipo/{r.Id}");
}
```

**Los catálogos de equipo usan los permisos de `equipos`**, no unos propios: no hay módulo
`catalogos` en `ClavesModulo` y no debe haberlo. Quien administra tipos y marcas administra
equipos. `puesto` usa `usuarios.*`; `tarifa` y `clausula` usan `rentas.*`.

**El traductor `AHttp`** es el único lugar del proyecto donde `RazonRechazo` se convierte en
código HTTP. Va en `Api/Comun/ResultadosHttp.cs` y se escribe en **R1**, antes que cualquier
controlador de negocio:

```csharp
internal static class ResultadosHttp
{
    public static IActionResult AHttp<T>(
        this ControllerBase c, Resultado<T> r, Func<T, string>? rutaCreado = null)
        => r switch
        {
            { Correcto: true } when rutaCreado is not null
                => c.Created(rutaCreado(r.Valor!), r.Valor),
            { Correcto: true } => c.Ok(r.Valor),
            { Razon: RazonRechazo.NoEncontrado } => c.Problem(
                title: "No encontrado", detail: r.Motivo,
                statusCode: StatusCodes.Status404NotFound),
            { Razon: RazonRechazo.Conflicto } => c.Problem(
                title: "Conflicto", detail: r.Motivo,
                statusCode: StatusCodes.Status409Conflict),
            _ => c.Problem(
                title: "Peticion rechazada", detail: r.Motivo,
                statusCode: StatusCodes.Status400BadRequest),
        };
}
```

Con esto un controlador nuevo no vuelve a escribir un `Problem(...)` a mano, y los códigos no
se pueden desalinear entre módulos.

**Las pruebas del servicio van contra Postgres real**, no contra InMemory: lo que hay que
probar de un catálogo es que el `UNIQUE` de `codigo` rechaza el duplicado y que un catálogo con
hijos no se borra, y esos son constraints que el proveedor InMemory no tiene. Las de un Proceso
sí van con dobles, como `RefrescoPruebas` hoy.

### 10.3 Inventario por rebanada

Archivos **nuevos de producción**, sin contar pruebas. `D` = archivo de DTO, `I` = interfaz,
`S` = servicio Ef, `P` = proceso, `C` = controlador.

| Rebanada | D | I | S | P | C | Notas |
|---|---:|---:|---:|---:|---:|---|
| **R1** transversales | 1 | 2 | 2 | — | 4 | `RequierePermiso` (3 archivos + el bucle), `InterceptorAuditoria`, `IAlmacenamientoArchivos` + `AlmacenamientoDisco`, `ResultadosHttp` |
| **R2** catálogos | 7 | 7 | 7 | — | 7 | los 7 idénticos salvo el filtro de texto y las FK de `modelo_equipo` |
| **R3** ubicaciones | 1 | 1 | 1 | — | 1 | las dos columnas generadas son solo de salida |
| **R4** trabajadores | 1 | 1 | 1 | — | 1 | `FormatoTelefono` |
| **R5** clientes y proveedores | 2 | 2 | 2 | — | 2 | `FormatoRfc`, `FormatoTelefono`, `FormatoCorreo` |
| **R6** equipos | 3 | 3 | 3 | 1 | 3 | `ProcesoSubirDocumentoEquipo` compone almacenamiento + `equipo_archivo` |
| **R7** disponibilidad | 2 | 2 | 2 | 1 | 2 | `ServicioOcupacionEf` traduce el `23P01`; `ProcesoTraspasarEquipo` |
| **R8** cotizaciones | 2 | 2 | 2 | 2 | 2 | `ProcesoCambiarEstadoCotizacion`, `ProcesoRecalcularTotales` |
| **R9** rentas | 4 | 3 | 3 | **5** | 3 | el corazón: confirmar, extender, cerrar, cancelar, desde-cotización |
| **R10** contratos | 2 | 1 | 1 | 2 | 1 | `ProcesoGenerarContrato` congela cláusulas; `ProcesoAutorizarContrato` |
| **R11** compra y venta | 4 | 2 | 2 | 4 | 2 | autorizar y finalizar × 2, simétricos |
| | **29** | **26** | **26** | **15** | **28** | **≈124 archivos de producción, más ~60 de pruebas** |

Más lo que no cae en ninguna columna: **una migración de `ContextoEmpresa`** con los cuatro
huecos de §6 —`MotivoOcupacion.Venta`, `EstadoContrato.Cancelado` y las secuencias de folios—,
que va **antes de R7** y termina con una corrida de `migrar-empresas`.

### 10.4 Los cinco Procesos de R9, que son el entregable

Todo lo demás de la fase es CRUD con reglas. Esto no:

| Proceso | Qué compone | La garantía que sostiene |
|---|---|---|
| `ProcesoConfirmarRenta` | rentas + ocupación + equipos | inserta **una fila de `ocupacion_equipo` por `renta_linea`**. Si el `EXCLUDE` rechaza una, **la transacción entera se deshace**: no existe una renta a medio confirmar |
| `ProcesoExtenderRenta` | rentas + ocupación | mueve el `fin` de las ocupaciones vigentes. El `EXCLUDE` revalida solo; si el equipo ya está tomado en las fechas nuevas, la extensión se rechaza con 409 |
| `ProcesoCerrarRenta` | rentas + ocupación | pone `activo = false` en las ocupaciones **sin borrar la fila**: el histórico de qué estuvo dónde se conserva |
| `ProcesoCancelarRenta` | rentas + ocupación | igual que cerrar, pero desde Borrador o Confirmada y sin fecha de devolución |
| `ProcesoRentaDesdeCotizacion` | cotizaciones + rentas | copia líneas y **precios congelados**, no los vuelve a leer del catálogo. Marca la cotización `Aceptada` |

**Los cinco abren transacción explícita.** Es la excepción a *"las transacciones se usan donde
el negocio las necesita, no como requisito de infraestructura"*: aquí el negocio las necesita,
porque una renta y su calendario son un solo hecho.

### 10.5 Qué se escribe primero, si hay que elegir

El orden de §5 es de dependencias; dentro de eso hay una prioridad de **riesgo**:

1. **R1 completo.** Los tres huecos transversales. Sin ellos todo lo demás se escribe dos veces.
2. **R2 con dos catálogos, no con siete.** Escribir `CategoriaEquipo` y `Marca`, revisarlos a
   fondo, y sólo entonces los otros cinco. Si al terminar los dos el código es literalmente
   idéntico salvo el tipo, **entonces** se extrae una base — no antes. Abstraer con un solo
   ejemplo es cómo se acaba con un genérico que no le queda a `Tarifa`.
3. **R7 antes que R8.** La disponibilidad es la pieza técnica de riesgo y la que puede obligar a
   cambiar el modelo. Descubrirlo con las cotizaciones ya escritas cuesta el doble.
4. **R9 al final del bloque comercial**, con R7 y R8 cerrados y probados contra la base real.

Y una regla de proceso: **cada rebanada se cierra con su corrida de pruebas contra Postgres
real antes de empezar la siguiente.** Con la API o Visual Studio abiertos hay que compilar a
otra carpeta —ver la nota de R0.1—, pero no se salta.
