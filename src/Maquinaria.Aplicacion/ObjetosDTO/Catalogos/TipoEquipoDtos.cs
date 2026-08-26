namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// Un tipo de equipo: excavadora, compactador, montacargas. Cuelga de una categoria.
/// </summary>
/// <param name="Categoria">
/// El nombre de la categoria, no solo su id. Va aqui porque toda pantalla que liste tipos lo
/// muestra, y resolverlo en el cliente costaria una llamada mas o una tabla en memoria.
/// </param>
public sealed record TipoEquipoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid CategoriaEquipoId,
    string Categoria,
    bool Activo,
    int Equipos);

public readonly record struct AltaTipoEquipo(
    Guid CategoriaEquipoId,
    string Codigo,
    string Nombre);

/// <summary>
/// Filtro propio: sobre el catalogo de tipos, la pregunta frecuente es «los de esta
/// categoria», y con el <see cref="Comun.Filtro"/> base habria que traerlos todos y filtrar
/// en el cliente.
/// </summary>
public sealed record FiltroTiposEquipo : Comun.Filtro
{
    public Guid? CategoriaEquipoId { get; init; }
}
