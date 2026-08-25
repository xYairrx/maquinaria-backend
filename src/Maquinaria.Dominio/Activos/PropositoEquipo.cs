namespace Maquinaria.Dominio.Activos;

/// <summary>
/// A que se destina la maquina. Lo pediste explicito: una compra "debera registrar en el
/// catalogo de equipo para poner a disposicion de renta o venta".
/// </summary>
public enum PropositoEquipo : short
{
    Renta = 1,

    Venta = 2,

    /// <summary>Se renta, y si aparece comprador tambien se vende.</summary>
    RentaYVenta = 3,
}
