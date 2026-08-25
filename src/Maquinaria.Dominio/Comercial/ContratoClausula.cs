namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Una clausula tal como quedo EN ESTE contrato.
///
/// EL TEXTO SE COPIA, no se referencia. Es la decision importante de esta tabla: si el
/// contrato solo guardara el id de la clausula del catalogo, editar el catalogo cambiaria
/// retroactivamente contratos ya firmados. Copiando el texto, cada contrato conserva lo
/// que de verdad se firmo.
///
/// <see cref="ClausulaId"/> queda solo como rastro de su origen, y es NULO cuando la
/// clausula se negocio con el cliente. Lo pediste asi: "las clausulas pueden ser del
/// catalogo general de clausulas o propias llegadas a acuerdos por el cliente".
/// </summary>
public class ContratoClausula
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ContratoId { get; set; }

    public Contrato? Contrato { get; set; }

    /// <summary>De donde salio. Nulo si es una clausula propia de este cliente.</summary>
    public Guid? ClausulaId { get; set; }

    public Clausula? Clausula { get; set; }

    public int Orden { get; set; }

    public required string Titulo { get; set; }

    /// <summary>El texto firmado. Copia, no referencia.</summary>
    public required string Texto { get; set; }

    public DateTime CreadoEn { get; set; }
}
