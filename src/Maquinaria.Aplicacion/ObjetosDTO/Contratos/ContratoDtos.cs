using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Contratos;

/// <summary>
/// El contrato de una renta. **Delgado, con las clausulas fuera.**
///
/// El M6 lista como «informacion» del contrato responsabilidades, combustible, danos y
/// penalizaciones. No son campos, son clausulas: aqui quedan partes, fechas, deposito y estado,
/// y los terminos viven en <c>contrato_clausula</c>.
///
/// LA CADENA ES <c>cotizacion → renta → contrato</c>, al reves de lo que dice la especificacion:
/// <c>contrato.renta_id</c> es obligatorio y **unico**. Un contrato por renta.
/// </summary>
public sealed record ContratoDto(
    Guid Id,
    string Folio,
    Guid RentaId,
    string RentaFolio,
    Guid ClienteId,
    string Cliente,
    DateOnly FechaInicio,
    DateOnly? FechaFin,
    decimal Deposito,
    EstadoContrato Estado,
    DateTime? FirmadoEn,
    string? Notas,
    IReadOnlyList<ContratoClausulaDto> Clausulas)
{
    /// <summary>
    /// Fuera de Borrador el contrato es inmutable, y lo impone un trigger. Se expone para que la
    /// pantalla deshabilite la edicion en lugar de dejar intentarlo y recibir un 409.
    /// </summary>
    public bool Editable => Estado == EstadoContrato.Borrador;
}

/// <summary>
/// Una clausula del contrato, con **su propia copia del titulo y del texto**.
///
/// <c>ClausulaId</c> es solo la referencia de donde salio, y es **nullable** por dos razones: la
/// clausula puede ser propia —negociada con ese cliente, sin plantilla— y la plantilla del
/// catalogo puede cambiar despues sin que este contrato se entere. Corregir el catalogo no
/// reescribe lo que alguien firmo.
/// </summary>
public sealed record ContratoClausulaDto(
    Guid Id,
    Guid? ClausulaId,
    int Orden,
    string Titulo,
    string Texto);

/// <param name="ClausulasDelCatalogo">
/// Las que se copian del catalogo. Si viene vacia, se copian **todas las obligatorias activas**:
/// es lo que se quiere el 90% de las veces y evita que un contrato salga sin la clausula de
/// penalizacion por olvido.
/// </param>
public readonly record struct AltaContrato(
    Guid RentaId,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    decimal Deposito,
    string? Notas,
    IReadOnlyList<Guid>? ClausulasDelCatalogo);

/// <summary>
/// Una clausula propia: se redacta en el contrato y no existe en el catalogo.
/// </summary>
public readonly record struct AltaContratoClausula(
    int Orden,
    string Titulo,
    string Texto);

public sealed record FiltroContratos : Comun.Filtro
{
    public Guid? ClienteId { get; init; }

    public EstadoContrato? Estado { get; init; }
}
