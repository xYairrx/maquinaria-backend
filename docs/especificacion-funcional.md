<!-- EXTRACCION MECANICA. No editar a mano. -->

# Especificación funcional — texto extraído

> **Este archivo no es la fuente de la verdad.** Es la extracción mecánica del texto de
> [`../Especificacion_Funcional_Software_Renta_Maquinaria.docx`](../Especificacion_Funcional_Software_Renta_Maquinaria.docx),
> que sí lo es. Existe para que la especificación se pueda **leer, buscar y citar sin abrir Word**,
> y para que quede en el historial de git junto al código que la implementa.
>
> Si el `.docx` cambia, hay que regenerar este archivo, no editarlo.

## Cómo leerlo

Dos advertencias que ahorran confusión:

1. **Es un documento escrito para UNA empresa, no para un producto SaaS.** Donde dice
   "la empresa", léase **el tenant**. No contempla las entidades de plataforma —Tenant,
   Plan, Módulo, Suscripción, límites, superadministrador, alta de clientes— que se
   diseñaron aparte. Ver [`01-arquitectura.md`](01-arquitectura.md).
2. **Define 26 módulos, no 30.** La numeración llega a 30 pero **M21, M22, M23 y M28 no
   existen**: el documento salta esos números. Cualquier referencia a "los 30 módulos" en
   el resto de la documentación es incorrecta.

Las flechas de los flujos se perdieron en la extracción; en el `.docx` aparecen como
símbolos que no sobreviven a texto plano. Donde se lee `renta  cierre  validación`, el
original dice `renta → cierre → validación`.

---

# ESPECIFICACIÓN FUNCIONAL

Versión 1.0 — Agosto 2026

## INTRODUCCIÓN

El presente documento define el funcionamiento general del sistema de gestión de renta de maquinaria y equipo.
El sistema está diseñado para empresas dedicadas a la renta de maquinaria pesada, maquinaria ligera, equipo de construcción, herramientas, vehículos, generadores y equipos especializados.
El objetivo es centralizar en una sola plataforma todo el ciclo de vida de los equipos y todas las operaciones relacionadas con su renta.
El sistema deberá permitir conocer en todo momento qué equipos posee la empresa, dónde se encuentran, en qué condiciones están, si están disponibles, quién los tiene rentados, cuánto tiempo llevan trabajando, cuánto han generado, cuánto han costado, cuándo requieren mantenimiento y qué rentabilidad generan.
El concepto principal es que cada equipo tenga un expediente digital completo, concentrando su información operativa, económica y de mantenimiento.

## ESTRUCTURA GENERAL DEL SISTEMA

Configuración: empresa, sucursales, patios, usuarios, roles, permisos, catálogos y parámetros generales. Operación comercial: clientes, cotizaciones, disponibilidad, contratos y rentas.
Administración de equipos: equipos, horómetros, kilometraje, documentos, evidencias e inspecciones. Logística: fletes, entregas, recolecciones, transporte, operadores y rutas.
Mantenimiento: mantenimiento preventivo, correctivo, órdenes de trabajo, refacciones, próximos servicios y proveedores.
Administración financiera: pagos, cobranza, costos, facturación y compras. Análisis: dashboard, reportes, KPI y rentabilidad.
Campo: PWA, inspecciones, entregas, devoluciones, evidencias, firmas y geolocalización.

## MÓDULO 1 — DASHBOARD

Objetivo: ser la pantalla principal de la empresa y mostrar rápidamente el estado actual del negocio. Debe mostrar equipos totales, disponibles, rentados, reservados, en mantenimiento y fuera de servicio. También debe mostrar rentas activas, próximas a vencer, vencidas y pendientes de entrega.
En finanzas: ingresos del periodo, cobranza pendiente, pagos vencidos y margen.
En mantenimiento: equipos próximos a servicio, mantenimiento vencido y equipos en taller. En operación: entregas, recolecciones y fletes activos.
El Dashboard no captura información directamente; concentra información proveniente de los demás módulos.

## MÓDULO 2 — EQUIPOS / CATÁLOGO DE ACTIVOS

Objetivo: registrar cada máquina o equipo que pertenece a la empresa.
Datos de identificación: ID, código interno, categoría, tipo, marca, modelo, número de serie y año. Información económica: costo de adquisición, valor actual, depreciación, tarifas por hora, día, semana y mes.
Información operativa: ubicación, estado, horómetro, kilometraje y fecha de adquisición. Documentación: factura, manual, póliza y documentos técnicos.
Multimedia: fotografías y videos.
Accesorios: accesorio, cantidad y descripción.

## EXPEDIENTE DIGITAL DEL EQUIPO

Cada equipo tendrá una ficha individual con información general, estado, ubicación, cliente actual, horómetro, próximo servicio y renta actual.
El usuario podrá consultar historial de rentas, mantenimientos, daños, fotografías, horómetros, ubicaciones, costos, ingresos y rentabilidad.
El equipo será el eje central de la información.

## MÓDULO 3 — DISPONIBILIDAD

Objetivo: determinar qué equipos pueden rentarse en una fecha determinada. El usuario selecciona equipo, fecha inicial y fecha final.
El sistema consulta automáticamente rentas, reservas, mantenimiento, bloqueos y traslados. Resultado: disponible o no disponible, mostrando el motivo cuando exista conflicto.
Regla fundamental: nunca permitir una doble asignación del mismo equipo.

## MÓDULO 4 — CLIENTES / CRM

Objetivo: administrar toda la información de los clientes.
Datos: nombre, razón social, RFC, teléfono, correo, contactos y domicilios.
Información comercial: límite de crédito, días de crédito, depósito requerido y condiciones especiales. Historial: cotizaciones, rentas, pagos, daños, incidencias y morosidad.
Semáforo del cliente: clasificación basada en puntualidad, saldos, daños, extensiones e historial de pago.

## MÓDULO 5 — COTIZACIONES

Objetivo: crear propuestas comerciales antes de generar una renta.
Flujo: nueva cotización  seleccionar cliente  seleccionar obra  seleccionar equipo  seleccionar fechas 
seleccionar tarifa  agregar servicios  calcular total  enviar cotización.
Puede incluir renta, flete, operador, horas incluidas, horas adicionales, depósito, extras, descuentos e impuestos. Estados: borrador, enviada, pendiente, aprobada, rechazada, vencida y cancelada.

## MÓDULO 6 — CONTRATOS

Objetivo: formalizar la operación después de que una cotización sea aceptada. El sistema toma automáticamente los datos de la cotización.
Información: cliente, equipo, obra, fechas, tarifa, horas incluidas, depósito, responsabilidades, combustible, daños, penalizaciones, firmas y anexos.

## MÓDULO 7 — RENTAS

Objetivo: administrar la operación real del equipo una vez que la cotización fue aceptada.
La renta conecta cliente, equipo, contrato, obra, logística, horómetro, pagos, mantenimiento y rentabilidad.
Datos: folio, cliente, equipo, contrato, obra, fechas, tarifa, cantidad, horas, depósito, anticipo, fletes, extras, daños, total y saldo.
Estados: borrador, reservada, preparación, en entrega, activa, en devolución, en inspección, pendiente de cargos, cerrada y cancelada.

## MÓDULO 8 — LOGÍSTICA Y FLETES

Objetivo: controlar entrega y recolección de equipos.
Datos: renta, equipo, origen, destino, fecha, hora, transporte, operador, ruta, costo y precio cobrado. Estados: pendiente  asignado  en tránsito  en sitio  entregado  finalizado.
Debe separar precio cobrado y costo real para calcular el margen del flete.

## MÓDULO 9 — INSPECCIÓN DE SALIDA

Objetivo: registrar el estado del equipo antes de entregarlo.
Checklist: motor, aceite, llantas, sistema hidráulico, carrocería, luces, accesorios, combustible, horómetro y daños. El operador puede tomar fotografías, agregar observaciones, registrar horómetro y firmar.
Resultado: equipo listo para entrega.

## MÓDULO 10 — INSPECCIÓN DE DEVOLUCIÓN

Objetivo: determinar el estado del equipo después de la renta.
Se recupera la inspección de salida y se realiza una nueva inspección.
Se comparan fotografías, daños, accesorios, combustible, horómetro y estado general. Resultado: sin novedades, daños nuevos, faltantes o horas excedentes.

## MÓDULO 11 — EVIDENCIAS

Objetivo: guardar fotografías, videos, documentos y firmas relacionados con las operaciones.
Cada evidencia debe asociarse a un evento: alta, entrega, devolución, mantenimiento, daño, flete o inspección. Debe guardar archivo, fecha, hora, usuario, ubicación, evento y comentario.

## MÓDULO 12 — HORÓMETROS Y KILOMETRAJE

Objetivo: controlar el uso real de los equipos.
Al entregar se registra horómetro inicial; durante la renta se pueden registrar lecturas periódicas y al devolver se registra el horómetro final.
El sistema calcula automáticamente horas utilizadas y horas excedentes conforme al contrato.

## MÓDULO 13 — MANTENIMIENTO

Objetivo: controlar todas las actividades de mantenimiento.
Se divide en preventivo, programado previamente, y correctivo, generado por una falla. El preventivo puede depender de fecha, horómetro, kilometraje o condición.
El correctivo se genera por falla o incidencia reportada.

## MÓDULO 14 — ÓRDENES DE TRABAJO

Objetivo: controlar cada trabajo realizado a un equipo.
Información: equipo, tipo, falla, diagnóstico, técnico, proveedor, fecha, horómetro, refacciones, mano de obra, costo, observaciones y evidencias.
Estados: solicitada  autorizada  en proceso  pendiente de refacciones  terminada  liberada.

## MÓDULO 15 — PRÓXIMO SERVICIO

Objetivo: determinar automáticamente cuándo deberá recibir servicio un equipo. Puede depender de fecha, horómetro, kilometraje o condición.
El sistema calcula horas o días restantes y genera alertas.

## MÓDULO 16 — INVENTARIO DE REFACCIONES

Objetivo: controlar las piezas utilizadas durante los mantenimientos.
Información: código, descripción, unidad, existencia, stock mínimo, costo y proveedor.
Las entradas aumentan inventario; el consumo en mantenimiento disminuye existencias y asigna el costo a la orden correspondiente.

## MÓDULO 17 — COMPRAS

Objetivo: controlar adquisiciones relacionadas con mantenimiento y operación.
Flujo: solicitud de compra  autorización  orden de compra  proveedor  recepción  entrada a inventario 
registro del costo.
Una compra puede relacionarse con mantenimiento, refacciones, equipo, obra o centro de costo.

## MÓDULO 18 — PROVEEDORES

Objetivo: administrar proveedores de refacciones, mantenimiento, fletes, subrentas y servicios. Datos: razón social, RFC, contactos, servicios, tarifas, historial, compras y pagos.

## MÓDULO 19 — PAGOS Y COBRANZA

Objetivo: controlar el dinero relacionado con las rentas.
Conceptos: anticipos, depósitos, pagos, pagos parciales, saldos y vencimientos. Los pagos actualizan automáticamente el saldo de la renta.

## MÓDULO 20 — FACTURACIÓN / INTEGRACIÓN FISCAL

Objetivo: preparar la información necesaria para facturar y conectarse posteriormente con un sistema fiscal.
Flujo: renta  cierre  validación fiscal  solicitud de factura  sistema fiscal  resultado  guardar documento fiscal
 relacionarlo con la renta.
No se recomienda desarrollar inicialmente un sistema contable completo.

## MÓDULO 24 — SUCURSALES Y PATIOS

Objetivo: controlar dónde se encuentra físicamente cada equipo. Se registran sucursales, patios y movimientos de equipos.
Una transferencia debe registrar equipo, origen, destino, fecha, usuario y motivo.

## MÓDULO 25 — USUARIOS Y PERMISOS

Objetivo: controlar quién puede consultar o modificar información.
Roles: administrador, dirección, ventas, rentas, logística, taller, operador, cobranza y cliente. Permisos por módulo: consulta, alta, edición, eliminación, autorización y exportación.

## MÓDULO 26 — NOTIFICACIONES

Objetivo: informar automáticamente sobre eventos importantes. Alertas de rentas: próxima a vencer, vencida y devolución pendiente.
Alertas de mantenimiento: próximo servicio, servicio vencido y equipo detenido. Alertas de cobranza: pago próximo y pago vencido.
Alertas de equipos: documento vencido y equipo improductivo.

## MÓDULO 27 — REPORTES

Reportes operativos: rentas, disponibilidad, equipos, entregas y devoluciones.
Reportes de mantenimiento: mantenimientos, costos, fallas, refacciones y tiempo en taller. Reportes financieros: ingresos, pagos, saldos, cobranza y costos.
Reportes de rentabilidad: equipo, cliente, obra, sucursal y flete.

## MÓDULO 29 — QR DE EQUIPOS

Cada equipo tendrá un QR único.
Al escanearlo se abrirá el expediente del equipo.
Según permisos se podrá consultar estado, renta, mantenimiento, manuales, documentos e historial.

## MÓDULO 30 — SUBRENTAS

Objetivo: permitir rentar a un cliente un equipo que la empresa no posee o no tiene disponible.
Flujo: solicitud del cliente  búsqueda de proveedor  registro de costo  precio al cliente  cálculo de margen 
seguimiento de la operación.

## FLUJO GENERAL ENTRE MÓDULOS

Cliente  Cotización  Disponibilidad  Contrato  Reserva  Renta  Inspección  Logística  Entrega  Operación  Horómetro  Mantenimiento  Devolución  Inspección  Daños/Extras  Cierre  Pago  Facturación  Rentabilidad  Historial del equipo.

## RELACIÓN ENTRE EQUIPO Y RENTA

Un equipo puede tener muchas rentas durante su vida.
Cada renta genera información que se acumula en el expediente del equipo: historial de rentas, ingresos, mantenimiento, daños, horómetros, costos y rentabilidad acumulada.

## RELACIÓN ENTRE RENTA Y CLIENTE

Un cliente puede tener múltiples rentas. El sistema debe acumularlas para calcular su comportamiento y rentabilidad.

## RELACIÓN ENTRE RENTA Y OBRA

Una obra puede tener múltiples rentas. Todas las operaciones económicas relacionadas deben acumularse en el centro de costo correspondiente.

## HISTORIAL DE CADA EQUIPO

El historial debe mostrar cronológicamente adquisición, alta, rentas, devoluciones, mantenimientos, daños, reparaciones y demás eventos importantes.

## REGLAS DE NEGOCIO PRINCIPALES

- Un equipo no puede tener dos rentas que se traslapen.
- Un equipo en mantenimiento no puede estar disponible.
- Un equipo fuera de servicio no puede rentarse.
- Una renta no puede cerrarse sin devolución.
- Una devolución debe tener inspección.
- Las horas excedentes se calculan automáticamente.
- Los daños se diferencian entre preexistentes y nuevos.
- Los pagos actualizan automáticamente el saldo.
- El consumo de refacciones afecta el inventario.
- Los costos de mantenimiento afectan la rentabilidad.
- Una extensión vuelve a verificar disponibilidad.
- Las modificaciones importantes quedan registradas en auditoría.

## AUDITORÍA

El sistema registra usuario, fecha, hora, acción, registro afectado, valor anterior y valor nuevo. Ejemplo: usuario Carlos modifica tarifa de 8,000 a 8,500.

## PRINCIPIO DE INTEGRACIÓN

Los módulos no deberán funcionar de manera aislada.
Una renta actualiza equipo, disponibilidad, calendario, logística, ingresos, rentabilidad e historial.
Un mantenimiento cambia el estado del equipo, genera costo, consume refacciones, actualiza el próximo servicio y modifica disponibilidad y rentabilidad.

## ORDEN RECOMENDADO DE DESARROLLO

FASE 1 — NÚCLEO: usuarios, roles, equipos, clientes, tarifas, disponibilidad, cotizaciones y rentas. FASE 2 — OPERACIÓN: contratos, logística, inspecciones, evidencias, horómetros y daños.
FASE 3 — TALLER: mantenimiento, órdenes de trabajo, refacciones, inventario, proveedores y próximo servicio. FASE 4 — FINANZAS: pagos, cobranza, compras, costos, facturación y rentabilidad.
FASE 5 — CAMPO: PWA, offline, GPS, firmas, geolocalización y QR.
FASE 6 — INTELIGENCIA: IA, predicción, pricing, recomendaciones y analítica avanzada.

## RESULTADO FINAL DEL SISTEMA

El sistema debe permitir conocer cuántos equipos existen, cuáles están disponibles, dónde está cada equipo, qué cliente lo tiene, cuándo regresa, cuánto ha trabajado, cuándo requiere mantenimiento, cuánto ha generado, cuánto ha costado, cuánto se gana realmente, qué clientes son rentables, qué obras son rentables, qué equipos están improductivos y cuánto dinero está pendiente de cobrar.

## CONCEPTO FINAL DEL PRODUCTO

El software no deberá entenderse únicamente como un sistema para rentar maquinaria.
Su función real será ser un SISTEMA INTEGRAL DE OPERACIÓN Y RENTABILIDAD DE ACTIVOS.
La renta será el proceso central y alrededor de ella deberán conectarse equipo, cliente, obra, logística, operación, mantenimiento, costos, pagos y rentabilidad.

## VISIÓN DEL SISTEMA

Cada equipo debe tener una historia digital completa: adquisición  registro  disponibilidad  reserva  renta  entrega  operación  horómetro  mantenimiento  devolución  inspección  cargos  pago  rentabilidad  nueva renta.
Este ciclo se repetirá durante toda la vida útil del activo.