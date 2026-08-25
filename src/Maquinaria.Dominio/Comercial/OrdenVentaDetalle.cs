using Maquinaria.Dominio.Activos;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Una maquina de la orden de venta.
///
/// SIN CANTIDAD, y no es un olvido: se vende una maquina concreta, identificada por su
/// id. Una cantidad aqui no tendria sentido —no hay "tres unidades" de la excavadora
/// numero 14— y abriria la puerta a vender dos veces la misma.
///
/// De hecho lo impide una restriccion: la misma maquina no puede repetirse en la misma
/// orden.
/// </summary>
public class OrdenVentaDetalle
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid OrdenVentaId { get; set; }

    public OrdenVenta? OrdenVenta { get; set; }

    public Guid EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal Importe { get; set; }

    public int Orden { get; set; }
}
