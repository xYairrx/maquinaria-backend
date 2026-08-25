using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Catalogos;

namespace Maquinaria.Dominio.Compras;

/// <summary>
/// Lo que se compra, renglon por renglon.
///
/// APUNTA A UN MODELO, NO A UN EQUIPO, y esa es la diferencia clave con la orden de
/// venta: al comprar la maquina TODAVIA NO EXISTE en el catalogo. Se pide "una
/// retroexcavadora modelo 320D", y solo cuando la orden se finaliza nace la fila de
/// equipo.
///
/// <see cref="EquipoId"/> es el puente entre los dos momentos: nulo mientras la orden
/// esta abierta, y con valor cuando ya se dio de alta la maquina. Lleva restriccion de
/// unicidad para que dos renglones no puedan reclamar el mismo equipo.
/// </summary>
public class OrdenCompraDetalle
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid OrdenCompraId { get; set; }

    public OrdenCompra? OrdenCompra { get; set; }

    /// <summary>Que se compra. El modelo, porque la maquina aun no existe.</summary>
    public Guid ModeloEquipoId { get; set; }

    public ModeloEquipo? ModeloEquipo { get; set; }

    /// <summary>La maquina que nacio de este renglon. Nulo hasta finalizar la orden.</summary>
    public Guid? EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    /// <summary>La serie que viene en la factura del proveedor.</summary>
    public string? NumeroSerie { get; set; }

    public int? Anio { get; set; }

    public int Cantidad { get; set; } = 1;

    public decimal CostoUnitario { get; set; }

    public decimal Importe { get; set; }

    public int Orden { get; set; }
}
