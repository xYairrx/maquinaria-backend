using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Comercio;

/// <summary>
/// Ordenes de compra de equipo. **Solo la base**: registrar el equipo en el catalogo al
/// finalizar lo hace el Proceso, porque son tres tablas.
///
/// El CHECK <c>orden_compra_finalizacion</c> exige que <c>estado = Finalizada</c> y
/// <c>finalizada_en</c> vayan juntos; este servicio los mueve siempre a la vez.
/// </summary>
public interface IServicioOrdenesCompra
{
    Task<Pagina<OrdenCompraDto>> ListarAsync(FiltroOrdenes filtro, CancellationToken ct);

    Task<OrdenCompraDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<OrdenCompraDto>> CrearAsync(AltaOrdenCompra alta, CancellationToken ct);

    Task<Resultado<OrdenCompraDetalleDto>> AgregarDetalleAsync(
        Guid ordenId, AltaOrdenCompraDetalle detalle, CancellationToken ct);

    Task<Resultado> QuitarDetalleAsync(Guid ordenId, Guid detalleId, CancellationToken ct);

    /// <summary>Autorizar o cancelar. Finalizar lo hace el Proceso.</summary>
    Task<Resultado<OrdenCompraDto>> CambiarEstadoAsync(
        Guid id, EstadoOrden estado, CancellationToken ct);

    /// <summary>Crea el equipo de un detalle y lo enlaza. Lo llama el Proceso.</summary>
    Task<Resultado<Guid>> RegistrarEquipoAsync(
        Guid ordenId, RegistroDeEquipo registro, CancellationToken ct);

    Task<Resultado<OrdenCompraDto>> MarcarFinalizadaAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Ordenes de venta de equipo.
///
/// **AL FINALIZAR, el equipo sale del parque y su calendario se cierra**, para que no pueda
/// rentarse despues. El alcance lo describe con <c>motivo = Venta</c> en
/// <c>ocupacion_equipo</c>; el enum migrado no tiene ese valor —el CHECK es
/// <c>BETWEEN 1 AND 6</c>—, asi que el Proceso lo cierra con <c>Bloqueo</c> y una nota que dice
/// de que venta salio. Es una adaptacion, no un descuido: esta anotada en el plan.
/// </summary>
public interface IServicioOrdenesVenta
{
    Task<Pagina<OrdenVentaDto>> ListarAsync(FiltroOrdenes filtro, CancellationToken ct);

    Task<OrdenVentaDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<OrdenVentaDto>> CrearAsync(AltaOrdenVenta alta, CancellationToken ct);

    Task<Resultado<OrdenVentaDetalleDto>> AgregarDetalleAsync(
        Guid ordenId, AltaOrdenVentaDetalle detalle, CancellationToken ct);

    Task<Resultado> QuitarDetalleAsync(Guid ordenId, Guid detalleId, CancellationToken ct);

    Task<Resultado<OrdenVentaDto>> CambiarEstadoAsync(
        Guid id, EstadoOrden estado, CancellationToken ct);

    /// <summary>Los equipos de la orden y su estado, que es lo que el Proceso necesita.</summary>
    Task<DatosDeVenta?> DatosDeVentaAsync(Guid id, CancellationToken ct);

    Task<Resultado> MarcarVendidosAsync(Guid id, CancellationToken ct);

    Task<Resultado<OrdenVentaDto>> MarcarFinalizadaAsync(Guid id, CancellationToken ct);
}

public sealed record DatosDeVenta(
    Guid Id,
    string Folio,
    EstadoOrden Estado,
    IReadOnlyList<Guid> EquipoIds);
