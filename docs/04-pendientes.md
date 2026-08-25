# Huecos de la especificación y decisiones pendientes

> Lo que hay que resolver con el negocio. No bloquea la Fase 0, pero sí bloquea partes de la Fase 1 y siguientes.
> Ordenado por urgencia.

---

## 1. Bloqueantes de la Fase 1

### 1.1 ~~Módulos faltantes en el documento~~ — resuelto el 2026-08-21

La numeración salta del **20 al 24** y del **27 al 29**, y durante un tiempo se asumió que
faltaban los módulos 21, 22, 23 y 28 de una versión incompleta del documento.

**No faltan: no existen.** El `.docx` entró al repositorio y define **26 módulos**, con esos
cuatro números simplemente sin usar. No hay ninguna entidad transversal escondida ahí, que
era el riesgo real que esta sección señalaba.

El catálogo de `modulo` en la base central ya tiene los 26, y `ClavesModulo` también.

### 1.2 Reglas de tarificación — el hueco más grande

El documento menciona *"tarifas por hora, día, semana y mes"*, *"horas incluidas"* y *"horas excedentes"*, pero **no especifica ninguna de las reglas de cálculo**. Es la parte con más lógica oculta de todo el sistema y donde estos productos se ganan o se pierden. Preguntas concretas:

- ¿Renta mínima? (¿un día completo aunque sean 3 horas?)
- ¿Los días son naturales o hábiles? ¿Cuentan sábados y domingos?
- Con tarifa mensual, ¿el mes es 30 días o de fecha a fecha?
- ¿Cuántas horas incluye un día de renta? (el estándar de la industria es 8, pero hay que confirmarlo)
- ¿Cómo se cobra la hora excedente? (¿tarifa hora normal, con recargo, con qué porcentaje?)
- ¿El sistema escoge automáticamente la tarifa más conveniente (12 días → tarifa semanal + días) o la elige el vendedor?
- ¿Cómo se prorratea una extensión? ¿Se recalcula toda la renta con la tarifa del nuevo plazo?
- ¿Cómo se cobra el día en que el equipo está en tránsito?

**Sin estas respuestas la Fase 1 no puede cerrarse.** Hay que sentarse con quien cotiza hoy y documentar los casos con ejemplos numéricos reales.

### 1.3 Depósitos y garantías

Aparecen en cotizaciones, contratos y rentas, pero no se define el ciclo: cuándo se cobra, dónde se registra mientras está retenido (es un pasivo, no un ingreso), contra qué se aplica al cierre, cómo se devuelve el saldo, y qué pasa si los daños exceden el depósito.

### 1.4 Combustible

Se menciona en contratos, inspecciones y checklist, pero sin política: ¿se entrega lleno y se devuelve lleno? ¿Se cobra la diferencia? ¿A qué precio — el del día, uno fijo, con margen?

---

## 2. Bloqueantes de fases posteriores

### 2.1 Carta Porte (Fase 4 — crítico para México)

El módulo 20 habla de facturación, pero **no menciona el complemento Carta Porte del CFDI**. En México, el traslado de maquinaria por carretera lo requiere obligatoriamente, y este sistema tiene un módulo entero de fletes (M8) con vehículos, operadores y rutas.

Esto **no es un detalle fiscal menor**: implica capturar y validar datos que hoy no están en el modelo —permiso SCT del transportista, configuración vehicular, placas, póliza de seguro de carga, licencia y RFC del operador, claves de producto SAT, ubicaciones con código postal de origen y destino, distancia recorrida—. Varios de esos campos hay que agregarlos a `Vehiculo`, `Operador` y `Flete`.

Conviene modelar `Vehiculo` y `Operador` con estos campos **desde la Fase 2**, aunque la emisión del complemento se implemente en la Fase 4. Agregarlos después significa migrar y recapturar.

### 2.2 ¿Un tenant puede tener varias razones sociales?

**La pregunta:** ¿un solo cliente suscrito opera con más de un RFC?

En México es común que los grupos de construcción y renta tengan varias razones sociales por motivos fiscales, compartiendo patios, personal y equipos. Si el mercado objetivo son grupos y no operadores individuales, hace falta una entidad `Empresa` entre `Tenant` y todo lo demás:

```
Tenant  ──<  Empresa  ──<  Sucursal  ──<  Patio
                   │
                   ├──< Equipo          (propiedad legal del activo)
                   └──< DocumentoFiscal (quién emite el CFDI, con qué CSD)
```

Ojo: **`Sucursal` no resuelve esto.** Una sucursal es una división *operativa*; una empresa es una entidad *legal*. Puede haber tres sucursales de una razón social, o una sucursal donde operan dos.

**Decisión tomada: se posterga.** Retrofitear `empresa_id` es barato, y con el modelo de base por empresa lo es todavía más: la tabla `empresa` viviría **dentro** de la base de cada cliente, así que agregarla es una migración normal más un relleno mecánico —se crea una `empresa` con los datos que ya están en `tenant` y se apunta todo a ella—.

No depende de ningún mecanismo de seguridad: es una llave foránea corriente dentro de una sola base.

Aplicando el criterio del proyecto —*adelanta lo que no se puede rellenar hacia atrás, posterga lo demás*— `empresa_id` es de los que se posponen.

**Cuándo resolverlo:** al planear la Fase 4 (facturación). Es el punto donde el modelo lo exige, porque el CFDI lo emite un RFC concreto con sus propios certificados CSD. Si la respuesta llega antes, mejor: la migración se aprovecha para meterlo junto con las tablas fiscales.

**Alternativa si la respuesta es "sí" pero se quiere evitar el modelo:** que el cliente contrate una suscripción por razón social. Es más simple para nosotros, pero le impide compartir equipos entre sus propias entidades y le duplica el costo — o sea, es una respuesta comercial mala.

### 2.3 Certificados fiscales por tenant

Al ser SaaS, cada empresa suscrita factura con **sus propios** certificados CSD (archivos `.cer` y `.key` más contraseña). Eso implica:

- almacenamiento cifrado de material criptográfico ajeno, por tenant,
- una responsabilidad legal y de seguridad considerable,
- decidir el PAC (Facturama, SW Sapien, Finkok…) y si el timbrado se hace con la cuenta de cada empresa o con una nuestra en modo multiemisor.

Es la decisión de mayor riesgo legal del producto. Vale la pena asesoría fiscal antes de diseñarlo.

### 2.4 Renta con operador — **REABIERTO el 2026-08-24**

> **Ojo con el historial de esta seccion.** El mismo dia se cerro con *"la renta es unicamente de equipo"* y se reabrio horas despues: **la renta SI puede incluir operador**. Lo que sigue vigente es la decision nueva.

**Alcance acotado, y eso es lo que lo hace viable:** se registra **quien va como operador** —un `trabajador`, con su puesto— y **cuanto se le cobra al cliente**. No se controlan jornadas, horas extra ni costo de mano de obra, asi que no se entra en terreno laboral.

Encaja limpio con el modelo de tarifas: el operador es **una linea de la renta con tarifa de operador**, mas el trabajador que va. No necesita tabla propia.

Lo que sigue pendiente si algun dia se quiere el control completo: costo de mano de obra, jornadas y horas extra — o sea, calcular el margen real del servicio.

#### Planteamiento original

Eso cierra la pregunta de si rentar con operador era un producto distinto —con costo de
mano de obra, control de jornadas, horas extra y obligaciones laborales— y quita esa
complejidad del modelo. El `Operador` del M8 sigue existiendo, pero solo como quien
**mueve** el equipo en un flete, no como parte de lo que se renta.

El texto original de esta seccion, para contexto:

#### Planteamiento original

El documento menciona "operador" en cotizaciones, contratos y logística, pero no aclara si la empresa **renta equipo con operador incluido** —que es un servicio distinto, con costo de mano de obra, control de jornadas, horas extra y obligaciones laborales— o si el operador solo mueve el equipo en el flete. Son dos productos diferentes.

### 2.5 Impuestos

No se especifica el manejo de IVA (16%, 8% en frontera), retenciones, ni el tratamiento fiscal de depósitos en garantía y penalizaciones. La renta de bienes muebles tiene reglas propias.

### 2.6 Manipulación de horómetro

El sistema calcula horas excedentes —y por tanto dinero— a partir de una lectura que **captura una persona en campo con su teléfono**. Sin controles, es un fraude trivial. Hace falta definir: validación de que la lectura nunca decrece, fotografía obligatoria del horómetro como respaldo, umbrales de variación que disparen una alerta, y quién puede corregir una lectura ya registrada.

### 2.7 Telemetría del fabricante

La Fase 5 contempla GPS propio, pero la maquinaria moderna ya reporta horómetro, ubicación y códigos de falla por su propia telemetría (Cat VisionLink, Komatsu Komtrax, John Deere JDLink). Integrarse con ellas eliminaría la captura manual de horómetros y habilitaría la Fase 6 (predicción de fallas) con datos reales. Vale evaluarlo como diferenciador.

---

## 3. Decisiones de producto SaaS pendientes

Son decisiones de negocio, no técnicas, pero condicionan el desarrollo:

| Tema | Pregunta |
|---|---|
| Identificación del tenant | ¿Subdominio (`empresa.tuapp.com`) o selector tras el login? El subdominio es mejor experiencia y mejor aislamiento, pero requiere DNS wildcard y certificado comodín |
| **Planes** | ¿Qué diferencia al Básico del Profesional? ¿Número de equipos, usuarios, o módulos habilitados? Si es por módulos, hace falta un sistema de *feature flags* por plan desde la Fase 0. **El mecanismo ya existe (2026-08-25):** el panel crea y retira planes desde `POST /api/plataforma/planes`, así que los precios reales ya no dependen de una migración. Lo que sigue abierto es la decisión COMERCIAL —qué planes hay, a qué precio y con qué módulos cada uno—, que es cosa de negocio y no de código. Y ojo: el precio todavía no se puede *editar*, porque `suscripcion` no guarda importe; ver `guias/estado-y-pendientes.md`. La migración `CentralSemillaCatalogos` siembra un único plan `base`, precio 0 y todos los límites en `-1`, únicamente para que el aprovisionamiento tenga a qué asociar la suscripción. No es catálogo comercial. Los precios reales **no** deben cargarse por migración —serían *append-only* y cambiar un precio exigiría un despliegue—, sino desde el panel de superadministrador |
| Prueba gratuita | ¿Autoservicio con tarjeta o alta manual? Define si hace falta pasarela de pago temprano |
| Cobro de suscripción | ¿Stripe, Mercado Pago, o facturación manual al inicio? |
| Portal de cliente | El rol `cliente` del M25 sugiere que los clientes de la empresa entran al sistema. ¿Es parte del producto base o un módulo aparte? |
| Migración de datos | Toda empresa que se suscriba ya tiene sus equipos en Excel. Un importador de catálogo desde Excel es probablemente el módulo con mejor retorno sobre esfuerzo de todo el proyecto |
| Región de despliegue | Datos de empresas mexicanas: conviene alojarlos en México o EE. UU. por latencia y por conversaciones de cumplimiento con clientes grandes |
| **Licencia perpetua** | Confirmado que se venderá también como copia permanente, no solo suscripción. Falta definir: ¿incluye hospedaje nuestro, instancia dedicada, o instalación en su infraestructura? ¿Lleva cuota anual de mantenimiento? ¿Cuántas versiones atrás se soportan? Las reglas de arquitectura que esto impone están en `01-arquitectura.md` §11 |
| **Alta de usuarios** | Confirmado: no hay registro público. Nosotros creamos el tenant y su primer administrador; ese administrador da de alta al resto de su empresa. Falta definir si el permiso `usuarios.crear` viene activo desde el inicio o se habilita después |
| **Recuperación de acceso total** | Cada empresa tiene **exactamente una** persona con acceso total, y si esa persona se va, solo la plataforma puede nombrar otra. Esa operación no existe todavía y hace falta decidir su forma: ¿la pide el cliente por soporte y la ejecutamos nosotros, o hay un botón en el panel? ¿Con qué doble comprobación, siendo que crea una cuenta con acceso total dentro de la base de un cliente? Subió de prioridad el **2026-08-25**, cuando el reintento del alta destapó que ese poder existía sin querer y sin control; ver `guias/estado-y-pendientes.md`. Es la decisión más delicada de la relación con un cliente, porque del lado técnico ya está claro que **no puede colgarse de ningún otro flujo** |

---

## 4. Riesgos técnicos identificados

| Riesgo | Mitigación |
|---|---|
| Volumen de multimedia (~48 mil fotos/año por empresa) domina el costo de infraestructura | Compresión en el cliente, miniaturas, política de retención, almacenamiento con egreso gratuito (Cloudflare R2) |
| Sincronización offline de la PWA con conflictos | Diseñar desde Fase 0 con IDs generados en cliente (uuid v7); las inspecciones son *append-only*, lo que evita la mayoría de los conflictos |
| Los reportes de rentabilidad tocan casi todas las tablas | `MovimientoCosto` como tabla única de costos (ver `02-modelo-datos.md` §3) + vistas materializadas |
| Concurrencia en la reserva de equipos | Resuelto por el constraint `EXCLUDE` a nivel de motor |
| Una consulta pesada de un tenant afecta a todos | Límites de statement timeout por rol, pool separado para reportes |
| Fuga de datos entre tenants | Base de datos independiente por empresa: el aislamiento es físico |
| Migraciones desalineadas entre bases | **Mitigado el 2026-08-25**: el comando `migrar-empresas` recorre todas las bases y es resistente a fallos parciales, y `GET /api/plataforma/salud/esquemas` reporta quién quedó atrasado. Sigue en pie la regla de nunca aplastar migraciones, que es lo que permite que la base que quedó atrás alcance |
| Aprovisionamiento a medias (registro sin base) | **Mitigado el 2026-08-25**: `tenant.estado_aprovisionamiento` ya dejaba el registro reintentable, y ahora existe quien lo reintenta — `POST /api/plataforma/empresas/{slug}/reintento`, que corre el mismo código del alta y solo actúa desde `Fallida` |

---

## 5. Decisiones evaluadas y descartadas

Registro de opciones que se consideraron y no se tomaron, con el motivo. Sirve para no volver a discutirlas desde cero, y para reabrirlas si cambian los supuestos.

### 5.1 SSO / servicio de identidad aparte — **descartado** (2026-08-17)

**Lo que se planteó:** un repositorio y servicio independiente para toda la autenticación.

**Motivos que se dieron y qué los cubre en realidad:**

| Motivo planteado | Qué lo resuelve | Estado |
|---|---|---|
| Llevar el control central de las empresas | Tablas de plataforma (`tenant`, `plan`, `suscripcion`) + panel de superadmin | Ya diseñado |
| Que una empresa que crezca pueda tener el software de forma dedicada | **Cada empresa ya tiene su propia base de datos.** Moverla a un servidor dedicado es cambiar su cadena de conexión | Inherente al modelo (`01-arquitectura.md` §2) |
| Enrutar al usuario a la instancia correcta al iniciar sesión | Un **directorio** de tenants: una tabla y un endpoint. No requiere OIDC | Punto de extensión previsto |
| Que un cliente entre con su directorio corporativo | Vía de login adicional sobre el JWT propio | Función de producto, Fase posterior |

**Por qué se descartó:**

1. **No hay un segundo producto que comparta usuarios.** Maquinaria se construye desde cero y no se integra con nada existente. Sin varias aplicaciones, no hay nada a lo cual hacer *single sign-on*.
2. **Multi-tenant no es SSO.** Muchas empresas usando un sistema no es lo mismo que muchos sistemas compartiendo login.
3. **Haría *más difícil* la instancia dedicada**, que era el motivo principal. Una instancia aislada debe ser autosuficiente; un SSO central le crea una dependencia dura de un servicio externo. Si ese servicio no está accesible, esa empresa no puede entrar a su propio sistema.
4. **Se perderían las llaves foráneas de `usuario_id`** en decenas de tablas (`auditoria`, `archivo.subido_por_id`, `inspeccion`, `orden_trabajo.tecnico_id`…), forzando una tabla espejo y sincronización.
5. **Retrasa la Fase 0**, que es el camino crítico al producto vendible.

**Cobertura tomada en su lugar**, para no cerrar la puerta:

- Cada empresa tiene su base propia desde el inicio, así que moverla a un servidor dedicado no requiere rediseño.
- El flujo de login ya resuelve la empresa antes de autenticar (campo "Empresa"), así que agregar otra forma de resolverla —subdominio, proveedor externo— es aditivo.

> **Se descartó también `usuario.sujeto_externo`.** Se había agregado como cobertura para identidad federada, pero al confirmarse que **no habrá registro público ni login con proveedores externos** (los usuarios se crean por invitación), la columna quedó sin justificación. Aplicando el criterio del proyecto: es una columna nullable sin relleno hacia atrás, o sea de las que se posponen. Si algún día hace falta, se agrega entonces.

**Costo de migrar después:** días, no meses. Se levanta el proveedor de identidad, se agrega la columna del identificador externo, y el endpoint de login cambia de validar contraseñas a validar tokens. **La matriz de permisos no se toca** — porque la autorización se quedó dentro de Maquinaria, que es la decisión que hace esto barato.

**Se reabre si:** aparece un segundo producto que necesite los mismos usuarios; o se requiere MFA, gestión de dispositivos y auditoría de sesiones a nivel organización.

**Nota sobre la variante de "interruptor":** usar un SSO central para conservar control sobre un cliente con instancia propia se resuelve mejor con licencias con vigencia y activación periódica, que es un mecanismo hecho para eso y no arriesga dejar al cliente sin acceso por una falla de red.
