namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// Una clausula del catalogo, que es la PLANTILLA de un termino contractual:
/// responsabilidades, combustible, danos, penalizaciones.
///
/// El M6 las lista como «informacion» del contrato. No son campos, son clausulas: con el
/// catalogo, el contrato se queda con partes, fechas, deposito y estado, y los terminos viven
/// donde deben.
/// </summary>
/// <param name="Obligatoria">
/// Si se copia sola al generar un contrato. Las de responsabilidad y penalizacion lo son.
/// </param>
public sealed record ClausulaDto(
    Guid Id,
    string Codigo,
    string Titulo,
    string Texto,
    int Orden,
    bool Obligatoria,
    bool Activo);

public readonly record struct AltaClausula(
    string Codigo,
    string Titulo,
    string Texto,
    int Orden,
    bool Obligatoria);

public sealed record FiltroClausulas : Comun.Filtro
{
    /// <summary>Solo las obligatorias, que es lo que pide el generador de contratos.</summary>
    public bool? Obligatoria { get; init; }
}
