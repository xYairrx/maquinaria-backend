using Maquinaria.Dominio.Organizacion;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Cada vez que se alarga una renta.
///
/// ES UN HISTORICO, y por eso es una tabla y no un simple UPDATE sobre renta.fin. Las
/// prorrogas son la norma en renta de maquinaria, y saber cuantas veces se alargo una
/// renta —y por que— es justo lo que hace falta para cobrarlas y para discutirlas con el
/// cliente. Un UPDATE a secas borraria esa historia.
///
/// Alargar una renta obliga a extender tambien la ocupacion del equipo, y ahi es donde la
/// restriccion EXCLUDE hace su trabajo: si otro ya tomo esas fechas, la prorroga se
/// rechaza en la base.
/// </summary>
public class ExtensionRenta
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RentaId { get; set; }

    public Renta? Renta { get; set; }

    public DateTime FinAnterior { get; set; }

    public DateTime FinNuevo { get; set; }

    public string? Motivo { get; set; }

    /// <summary>Quien autorizo la prorroga.</summary>
    public Guid TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public DateTime CreadoEn { get; set; }
}
