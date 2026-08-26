using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Comercio;

/// <summary>
/// Una orden de compra de equipo. **Mismo flujo que la venta, simetrico**:
/// Borrador → Autorizada → Finalizada, mas Cancelada.
///
/// **AL FINALIZAR, el equipo se registra en el catalogo** y queda a disposicion de renta o de
/// venta. Ese es el punto de la orden: es como entra maquinaria al parque.
/// </summary>
public sealed record OrdenCompraDto(
    Guid Id,
    string Folio,
    Guid ProveedorId,
    string Proveedor,
    Guid TrabajadorId,
    string Trabajador,
    DateOnly Fecha,
    EstadoOrden Estado,
    decimal Subtotal,
    decimal Impuestos,
    decimal Total,
    DateTime? AutorizadaEn,
    DateTime? FinalizadaEn,
    string? Notas,
    IReadOnlyList<OrdenCompraDetalleDto> Detalles);

/// <param name="EquipoId">
/// El equipo que esta linea produjo, y solo se llena al finalizar. Antes es nulo: la maquina
/// todavia no existe en el catalogo.
/// </param>
public sealed record OrdenCompraDetalleDto(
    Guid Id,
    Guid ModeloEquipoId,
    string Marca,
    string Modelo,
    Guid? EquipoId,
    string? CodigoInterno,
    string? NumeroSerie,
    int? Anio,
    int Cantidad,
    decimal CostoUnitario,
    decimal Importe,
    int Orden);

public readonly record struct AltaOrdenCompra(
    Guid ProveedorId,
    Guid TrabajadorId,
    DateOnly? Fecha,
    decimal Impuestos,
    string? Notas);

/// <param name="Cantidad">
/// **Tiene que ser 1 si la linea va a registrar un equipo**, y es una adaptacion al esquema
/// migrado: <c>orden_compra_detalle</c> tiene un solo <c>equipo_id</c> con indice unico, asi que
/// una linea no puede producir tres maquinas. Tres excavadoras iguales son tres lineas — que
/// ademas es lo correcto, porque cada una tiene su numero de serie.
/// </param>
public readonly record struct AltaOrdenCompraDetalle(
    Guid ModeloEquipoId,
    string? NumeroSerie,
    int? Anio,
    int Cantidad,
    decimal CostoUnitario,
    int Orden);

/// <param name="CodigoInterno">
/// El codigo con el que la maquina entra al catalogo. Se pide al finalizar y no al capturar la
/// linea porque es una decision de inventario, no de compra.
/// </param>
public readonly record struct RegistroDeEquipo(
    Guid DetalleId,
    string CodigoInterno,
    Guid TipoEquipoId,
    Guid? UbicacionId);

/// <summary>Una orden de venta de equipo. Al finalizar, el equipo sale del parque.</summary>
public sealed record OrdenVentaDto(
    Guid Id,
    string Folio,
    Guid ClienteId,
    string Cliente,
    Guid TrabajadorId,
    string Trabajador,
    DateOnly Fecha,
    EstadoOrden Estado,
    decimal Subtotal,
    decimal Descuento,
    decimal Impuestos,
    decimal Total,
    DateTime? AutorizadaEn,
    DateTime? FinalizadaEn,
    string? Notas,
    IReadOnlyList<OrdenVentaDetalleDto> Detalles);

public sealed record OrdenVentaDetalleDto(
    Guid Id,
    Guid EquipoId,
    string CodigoInterno,
    string Modelo,
    decimal PrecioUnitario,
    decimal Importe,
    int Orden);

public readonly record struct AltaOrdenVenta(
    Guid ClienteId,
    Guid TrabajadorId,
    DateOnly? Fecha,
    decimal Descuento,
    decimal Impuestos,
    string? Notas);

public readonly record struct AltaOrdenVentaDetalle(
    Guid EquipoId,
    decimal PrecioUnitario,
    int Orden);

public sealed record FiltroOrdenes : Comun.Filtro
{
    public EstadoOrden? Estado { get; init; }

    /// <summary>Proveedor en compras, cliente en ventas.</summary>
    public Guid? ContraparteId { get; init; }
}
