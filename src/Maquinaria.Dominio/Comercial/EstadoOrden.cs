namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// El ciclo de una orden de compra o de venta: "se genera, se autoriza y se finaliza",
/// como lo describiste.
///
/// EL VALOR 3 ES ESPECIAL: la base exige que finalizada_en tenga fecha exactamente cuando
/// el estado es Finalizada, y que este vacia en cualquier otro. Sin eso, una orden podria
/// decirse finalizada sin fecha, o traer fecha sin estarlo, y el reporte de compras del
/// mes mentiria.
///
/// Compartido por compra y venta a proposito: es el mismo ciclo, y duplicar el enum haria
/// que un dia divergieran sin motivo.
/// </summary>
public enum EstadoOrden : short
{
    Borrador = 1,

    Autorizada = 2,

    /// <summary>
    /// Finalizada. En una compra, es el momento en que el equipo entra al catalogo.
    /// </summary>
    Finalizada = 3,

    Cancelada = 4,
}
