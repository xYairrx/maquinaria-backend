using Maquinaria.Dominio.Activos;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// UNA MAQUINA rentada, con la tarifa a la que se cobra.
///
/// La diferencia con <see cref="RentaConcepto"/> te la explique y la confirmaste: aqui va
/// lo que se renta —equipo obligatorio—, alla va lo que se cobra ademas —flete, operador,
/// mantenimiento—, que no tiene equipo propio.
///
/// Lleva los horometros de salida y devolucion porque son la evidencia de cuanto se uso
/// la maquina. En Fase 1 solo se capturan; el cobro por hora excedida es Fase 2.
/// </summary>
public class RentaLinea
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RentaId { get; set; }

    public Renta? Renta { get; set; }

    public Guid EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    /// <summary>Por hora, por dia, por semana. Sale del catalogo de tarifas.</summary>
    public Guid TarifaId { get; set; }

    public Tarifa? Tarifa { get; set; }

    public decimal Cantidad { get; set; } = 1;

    public decimal PrecioUnitario { get; set; }

    /// <summary>Las horas que la tarifa incluye antes de cobrar excedente.</summary>
    public decimal? HorasIncluidas { get; set; }

    public decimal Importe { get; set; }

    public decimal? HorometroSalida { get; set; }

    public decimal? HorometroDevolucion { get; set; }

    public int Orden { get; set; }
}
