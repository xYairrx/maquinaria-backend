using Maquinaria.Dominio.Terceros;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// El documento legal de una renta, con sus clausulas.
///
/// CUELGA DE LA RENTA, NO DE LA COTIZACION. Lo corregiste: "el contrato va sobre renta y
/// no sobre cotizacion". Y es lo correcto: una cotizacion puede no aceptarse nunca, y un
/// contrato sobre algo que no se cerro no significa nada.
///
/// SE VUELVE INMUTABLE AL SALIR DE BORRADOR. No lo cuida la aplicacion: lo cuida un
/// disparador que rechaza UPDATE y DELETE en cuanto el estado deja de ser 1. Un contrato
/// que cambia despues de firmado no es un contrato, y esa garantia tiene que vivir donde
/// no se pueda saltar por error.
///
/// UNA RESTRICCION QUE CONVIENE QUE CONOZCAS: <c>UNIQUE (renta_id)</c> significa un
/// contrato por renta. Si algun cliente firma un contrato marco que cubra varias rentas,
/// hay que quitarla. Se deja porque quitarla despues es trivial y ponerla despues ya no
/// se puede si hay datos que la violan.
/// </summary>
public class Contrato
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Folio { get; set; }

    public Guid RentaId { get; set; }

    public Renta? Renta { get; set; }

    /// <summary>
    /// Repetido respecto a la renta A PROPOSITO: el contrato es un documento legal y
    /// tiene que decir a nombre de quien esta sin depender de que la renta siga igual.
    /// </summary>
    public Guid ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly? FechaFin { get; set; }

    public decimal Deposito { get; set; }

    public EstadoContrato Estado { get; set; } = EstadoContrato.Borrador;

    public DateTime? FirmadoEn { get; set; }

    public string? Notas { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }

    public ICollection<ContratoClausula> Clausulas { get; set; } = [];
}
