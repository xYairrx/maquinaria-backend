namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// Un puesto de la organizacion: operador, mecanico, chofer, vendedor.
///
/// Es catalogo de TRABAJADORES, no de usuarios. Un trabajador es una persona con un puesto; un
/// usuario es una cuenta. El operador de patio puede no tener acceso al sistema y hay que
/// poder registrarlo igual.
/// </summary>
public sealed record PuestoDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    bool Activo,
    int Trabajadores);

public readonly record struct AltaPuesto(string Codigo, string Nombre, string? Descripcion);
