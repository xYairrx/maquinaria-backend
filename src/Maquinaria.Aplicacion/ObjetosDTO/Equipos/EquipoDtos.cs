using Maquinaria.Dominio.Activos;

namespace Maquinaria.Aplicacion.Equipos;

/// <summary>
/// Un equipo del parque. Es la entidad central de la fase: lo que se renta, lo que se traspasa
/// y lo que se vende.
///
/// <c>equipo</c> **no tiene <c>proveedor_id</c>** —se quito el 2026-08-25—: el proveedor vive
/// en la orden de compra y desde aqui se alcanza por
/// <c>equipo → orden_compra_detalle → orden_compra → proveedor</c>.
/// </summary>
/// <param name="Proposito">
/// Renta, Venta o las dos. **Hay un solo ciclo de vida del equipo**, que puede terminar en
/// venta: no existen parques separados de renta y de venta.
/// </param>
public sealed record EquipoDto(
    Guid Id,
    string CodigoInterno,
    Guid ModeloEquipoId,
    string Marca,
    string Modelo,
    Guid TipoEquipoId,
    string TipoEquipo,
    Guid? UbicacionId,
    string? Ubicacion,
    string? NumeroSerie,
    int? Anio,
    EstadoEquipo Estado,
    PropositoEquipo Proposito,
    OrigenEquipo Origen,
    DateOnly? FechaAdquisicion,
    decimal? CostoAdquisicion,
    decimal? ValorActual,
    decimal? Horometro,
    decimal? Kilometraje,
    string? Notas,
    int Documentos,
    int PreciosVigentes);

/// <summary>
/// <c>Estado</c> NO esta aqui: nace Disponible y se mueve con su propia accion o con los
/// Procesos —confirmar una renta lo pone Rentado, finalizar una venta lo pone Vendido—. Si el
/// PUT lo aceptara, una correccion de notas podria sacar de la calle una maquina rentada.
/// </summary>
public readonly record struct AltaEquipo(
    string CodigoInterno,
    Guid ModeloEquipoId,
    Guid TipoEquipoId,
    Guid? UbicacionId,
    string? NumeroSerie,
    int? Anio,
    PropositoEquipo Proposito,
    OrigenEquipo Origen,
    DateOnly? FechaAdquisicion,
    decimal? CostoAdquisicion,
    decimal? ValorActual,
    decimal? Horometro,
    decimal? Kilometraje,
    string? Notas);

public readonly record struct CambioEstadoEquipo(EstadoEquipo Estado, string? Nota);

public sealed record FiltroEquipos : Comun.Filtro
{
    public Guid? UbicacionId { get; init; }

    public Guid? TipoEquipoId { get; init; }

    public Guid? ModeloEquipoId { get; init; }

    public EstadoEquipo? Estado { get; init; }

    public PropositoEquipo? Proposito { get; init; }
}
