namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// Una categoria del catalogo de equipos: el nivel mas alto de la clasificacion
/// —excavacion, compactacion, izaje— del que cuelgan los tipos.
/// </summary>
/// <param name="Tipos">
/// Cuantos tipos de equipo cuelgan de ella. Va en el DTO porque es lo que decide si la
/// pantalla ofrece desactivarla sin dejar tipos huerfanos, y contarlo aqui cuesta un
/// subquery en lugar de una llamada por fila.
/// </param>
public sealed record CategoriaEquipoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    bool Activo,
    int Tipos);

/// <summary>
/// El cuerpo del alta y de la edicion. **El mismo tipo para las dos** a proposito: los
/// campos capturables son exactamente los mismos, y dos records identicos se
/// desincronizan en cuanto alguien agregue una columna a uno.
///
/// <c>Activo</c> NO esta aqui: se cambia con su propia accion. Si viniera en la edicion, un
/// PUT que solo quiere corregir una falta de ortografia podria reactivar una categoria
/// retirada sin que nadie lo pidiera.
/// </summary>
public readonly record struct AltaCategoriaEquipo(
    string Codigo,
    string Nombre,
    string? Descripcion);
