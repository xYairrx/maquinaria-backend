using Maquinaria.Dominio.Comercial;
using Maquinaria.Dominio.Organizacion;
using Maquinaria.Dominio.Terceros;

namespace Maquinaria.Dominio.Compras;

/// <summary>
/// La compra de equipo. "Se maneja un proceso no tan tedioso: se genera una orden de
/// compra y se autoriza y finaliza", tal como lo pediste.
///
/// AL FINALIZARSE, EL EQUIPO ENTRA AL CATALOGO. Es el otro requisito que diste: la compra
/// "debera registrar en el catalogo de equipo para poner a disposicion de renta o venta".
/// El vinculo lo guarda <see cref="OrdenCompraDetalle.EquipoId"/>, que sigue nulo hasta
/// que la orden se finaliza.
///
/// ES LA UNICA DUENA DEL PROVEEDOR. Cuando quitaste proveedor_id de equipo, de quien se
/// compro paso a ser un hecho de esta orden y de ningun otro lugar.
/// </summary>
public class OrdenCompra
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Folio { get; set; }

    public Guid ProveedorId { get; set; }

    public Proveedor? Proveedor { get; set; }

    public Guid TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public DateOnly Fecha { get; set; }

    public EstadoOrden Estado { get; set; } = EstadoOrden.Borrador;

    public decimal Subtotal { get; set; }

    public decimal Impuestos { get; set; }

    public decimal Total { get; set; }

    public DateTime? AutorizadaEn { get; set; }

    public DateTime? FinalizadaEn { get; set; }

    public string? Notas { get; set; }

    public DateTime CreadoEn { get; set; }

    public ICollection<OrdenCompraDetalle> Detalles { get; set; } = [];
}
