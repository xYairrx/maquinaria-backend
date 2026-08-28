namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// Un modelo concreto: «336D2», «PC200-8». Cuelga de una marca y, opcionalmente, de un tipo.
/// </summary>
/// <param name="TipoEquipoId">
/// Nulo se permite: un modelo se puede capturar antes de decidir su tipo, y el tipo real de
/// cada equipo vive en <c>equipo.tipo_equipo_id</c>, que si es obligatorio. Aqui es una ayuda
/// de captura, no la fuente de la verdad.
/// </param>
/// <param name="HorasEntreServicios">
/// Lo usara el mantenimiento preventivo en la Fase 3. Se captura desde ahora porque es un dato
/// del modelo, no de la operacion, y llenarlo despues para un parque entero es trabajo manual.
/// </param>
public sealed record ModeloEquipoDto(
    Guid Id,
    Guid MarcaId,
    string Marca,
    Guid? TipoEquipoId,
    string? TipoEquipo,
    string Nombre,
    string? Descripcion,
    int? HorasEntreServicios,
    bool Activo,
    int Equipos);

public readonly record struct AltaModeloEquipo(
    Guid MarcaId,
    Guid? TipoEquipoId,
    string Nombre,
    string? Descripcion,
    int? HorasEntreServicios);

public sealed record FiltroModelosEquipo : Comun.Filtro
{
    public Guid? MarcaId { get; init; }

    public Guid? TipoEquipoId { get; init; }
}
