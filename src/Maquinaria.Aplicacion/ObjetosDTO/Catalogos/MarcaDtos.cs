namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// Una marca de maquinaria: Caterpillar, Komatsu, JCB.
/// </summary>
/// <param name="Modelos">Cuantos modelos cuelgan de ella. Mismo papel que Tipos en la categoria.</param>
public sealed record MarcaDto(Guid Id, string Nombre, bool Activo, int Modelos);

/// <summary>
/// UN SOLO CAMPO, y por eso no hay un DTO generico de catalogo: <c>marca</c> no tiene codigo
/// ni descripcion —su identidad ES el nombre, con UNIQUE sobre el—, mientras
/// <c>categoria_equipo</c> tiene codigo, nombre y descripcion, y <c>modelo_equipo</c> cuelga
/// de dos llaves foraneas.
///
/// Los siete catalogos de la rebanada se parecen en la FORMA de sus operaciones, no en sus
/// campos. Ver la nota de §10.5 del plan sobre no abstraer con un solo ejemplo.
/// </summary>
public readonly record struct AltaMarca(string Nombre);
