using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Cotizaciones;

/// <summary>
/// Una propuesta comercial. **No reserva nada**: no escribe en <c>ocupacion_equipo</c>, y por eso
/// sus lineas van en UNA tabla y pueden referenciar un tipo de equipo en lugar de un equipo
/// concreto —«una excavadora de 20 t» antes de saber cual—.
/// </summary>
public sealed record CotizacionDto(
    Guid Id,
    string Folio,
    Guid ClienteId,
    string Cliente,
    Guid UbicacionId,
    string Ubicacion,
    Guid TrabajadorId,
    string Trabajador,
    DateOnly Fecha,
    DateOnly? VigenciaHasta,
    EstadoCotizacion Estado,
    decimal Subtotal,
    decimal Descuento,
    decimal Impuestos,
    decimal Total,
    string? Notas,
    IReadOnlyList<CotizacionLineaDto> Lineas);

/// <summary>
/// Una linea. La define su TARIFA; el equipo y el tipo son contexto opcional.
///
/// **Los dos pueden venir nulos**, y eso importa: una linea de flete no tiene equipo ni tipo. El
/// CHECK que exigia uno de los dos se quito el 2026-08-25 porque hacia imposible cotizar un
/// flete.
/// </summary>
public sealed record CotizacionLineaDto(
    Guid Id,
    Guid TarifaId,
    string Tarifa,
    string Unidad,
    Guid? EquipoId,
    string? Equipo,
    Guid? TipoEquipoId,
    string? TipoEquipo,
    string? Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal Importe,
    int Orden);

/// <summary>
/// El folio NO va aqui: lo genera el sistema. Aceptarlo dejaria que dos capturistas eligieran
/// el mismo y que alguien saltara la numeracion.
/// </summary>
public readonly record struct AltaCotizacion(
    Guid ClienteId,
    Guid UbicacionId,
    Guid TrabajadorId,
    DateOnly? Fecha,
    DateOnly? VigenciaHasta,
    decimal Descuento,
    decimal Impuestos,
    string? Notas);

/// <param name="PrecioUnitario">
/// **Se captura, no se calcula.** La fase no escoge la tarifa conveniente ni decide si doce dias
/// son semana mas dias: un vendedor captura el importe que acordo y el documento lo conserva. El
/// importe si se calcula: cantidad por precio.
/// </param>
public readonly record struct AltaCotizacionLinea(
    Guid TarifaId,
    Guid? EquipoId,
    Guid? TipoEquipoId,
    string? Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    int Orden);

public sealed record FiltroCotizaciones : Comun.Filtro
{
    public Guid? ClienteId { get; init; }

    public EstadoCotizacion? Estado { get; init; }

    public DateOnly? Desde { get; init; }

    public DateOnly? Hasta { get; init; }
}
