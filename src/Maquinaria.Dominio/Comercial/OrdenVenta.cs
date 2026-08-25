using Maquinaria.Dominio.Organizacion;
using Maquinaria.Dominio.Terceros;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// La venta de una maquina. "Se genera una orden de venta de un equipo, se autoriza y se
/// finaliza", con su formato y sus detalles.
///
/// EL CLIENTE ES EL MISMO QUE EL DE RENTAS. Era tu requisito: seguir al cliente "a quien
/// se le renta y si llega a ser comprador de equipo". Una tabla de compradores aparte
/// haria imposible ver de un tiro que un cliente renta y compra.
/// </summary>
public class OrdenVenta
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Folio { get; set; }

    public Guid ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public Guid TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public DateOnly Fecha { get; set; }

    public EstadoOrden Estado { get; set; } = EstadoOrden.Borrador;

    public decimal Subtotal { get; set; }

    public decimal Descuento { get; set; }

    public decimal Impuestos { get; set; }

    public decimal Total { get; set; }

    public DateTime? AutorizadaEn { get; set; }

    public DateTime? FinalizadaEn { get; set; }

    public string? Notas { get; set; }

    public DateTime CreadoEn { get; set; }

    public ICollection<OrdenVentaDetalle> Detalles { get; set; } = [];
}
