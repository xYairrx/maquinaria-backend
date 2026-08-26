using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Rentas;

/// <summary>
/// La operacion real. **Es el criterio de salida de la fase**: cotizar → aprobar → rentar →
/// cerrar, y es imposible rentar dos veces el mismo equipo en fechas traslapadas.
///
/// DONDE SE TRABAJA VA AQUI DENTRO, no en una tabla obra: <c>lugar_descripcion</c> es
/// obligatoria y los campos de direccion son opcionales. El precio de esa simplificacion esta
/// dicho en el alcance: no se puede agrupar rentabilidad por obra de forma confiable.
/// </summary>
public sealed record RentaDto(
    Guid Id,
    string Folio,
    Guid ClienteId,
    string Cliente,
    Guid? CotizacionId,
    string? CotizacionFolio,
    Guid TrabajadorId,
    string Trabajador,
    DateTime Inicio,
    DateTime Fin,
    EstadoRenta Estado,
    LugarRenta Lugar,
    decimal Deposito,
    decimal Anticipo,
    decimal Subtotal,
    decimal Descuento,
    decimal Impuestos,
    decimal Total,
    decimal Saldo,
    string? Notas,
    IReadOnlyList<RentaLineaDto> Lineas,
    IReadOnlyList<RentaConceptoDto> Conceptos)
{
    /// <summary>
    /// Estado DERIVADO de la fecha, no almacenado: la renta activa cuyo fin ya paso esta
    /// vencida, y la que vence en tres dias esta por vencer. Guardarlo exigiria un proceso que
    /// recorriera la tabla cada noche; calcularlo aqui no puede quedar desactualizado.
    /// </summary>
    public bool Vencida => Estado == EstadoRenta.Activa && Fin < DateTime.UtcNow;

    public bool PorVencer => Estado == EstadoRenta.Activa
                             && !Vencida
                             && Fin < DateTime.UtcNow.AddDays(3);
}

public readonly record struct LugarRenta(
    string Descripcion,
    string? Calle,
    string? Colonia,
    string? Municipio,
    string? EstadoProv,
    string? CodigoPostal,
    decimal? Latitud,
    decimal? Longitud,
    string? Contacto,
    string? Telefono);

/// <summary>
/// **Lo que se renta.** Una fila por equipo, <c>equipo_id</c> obligatorio, y es **lo unico que
/// genera filas de <c>ocupacion_equipo</c>**: dos equipos, dos filas de calendario.
/// </summary>
public sealed record RentaLineaDto(
    Guid Id,
    Guid EquipoId,
    string CodigoInterno,
    string Modelo,
    Guid TarifaId,
    string Tarifa,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal? HorasIncluidas,
    decimal Importe,
    decimal? HorometroSalida,
    decimal? HorometroDevolucion,
    int Orden);

/// <summary>
/// **Lo que se cobra ademas.** No lleva equipo: flete, operador, maniobras.
///
/// El <c>Costo</c> va aparte del importe porque el documento lo pide explicitamente para el
/// flete: el margen es la resta.
/// </summary>
public sealed record RentaConceptoDto(
    Guid Id,
    Guid TarifaId,
    string Tarifa,
    Guid? TrabajadorId,
    string? Trabajador,
    string? Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal? Costo,
    decimal Importe);

public sealed record ExtensionRentaDto(
    Guid Id,
    DateTime FinAnterior,
    DateTime FinNuevo,
    string? Motivo,
    Guid TrabajadorId,
    string Trabajador,
    DateTime CreadoEn);

public readonly record struct AltaRenta(
    Guid ClienteId,
    Guid? CotizacionId,
    Guid TrabajadorId,
    DateTime Inicio,
    DateTime Fin,
    LugarRenta Lugar,
    decimal Deposito,
    decimal Anticipo,
    decimal Descuento,
    decimal Impuestos,
    string? Notas);

public readonly record struct AltaRentaLinea(
    Guid EquipoId,
    Guid TarifaId,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal? HorasIncluidas,
    int Orden);

public readonly record struct AltaRentaConcepto(
    Guid TarifaId,
    Guid? TrabajadorId,
    string? Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal? Costo);

/// <param name="FinNuevo">Tiene que ser posterior al fin actual: una extension avanza.</param>
public readonly record struct AltaExtension(
    DateTime FinNuevo,
    Guid TrabajadorId,
    string? Motivo);

/// <param name="HorometrosDevolucion">
/// Lectura de horometro por linea al devolver. Opcional: no todos los equipos lo llevan, y el
/// modulo de horometros es M12, Fase 2.
/// </param>
public readonly record struct CierreDeRenta(
    Dictionary<Guid, decimal>? HorometrosDevolucion,
    string? Nota);

public sealed record FiltroRentas : Comun.Filtro
{
    public Guid? ClienteId { get; init; }

    public Guid? EquipoId { get; init; }

    public EstadoRenta? Estado { get; init; }

    public DateTime? Desde { get; init; }

    public DateTime? Hasta { get; init; }
}
