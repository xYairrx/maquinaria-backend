using Maquinaria.Dominio.Organizacion;

namespace Maquinaria.Aplicacion.Organizacion;

/// <summary>
/// Una persona de la organizacion.
///
/// UN TRABAJADOR NO ES UN USUARIO. El trabajador es la persona —quien opera la maquina, quien
/// levanta la renta—; el usuario es la cuenta. El operador de patio puede no tener acceso al
/// sistema y hay que poder registrarlo igual. <c>UsuarioId</c> es el enlace opcional entre los
/// dos, con indice unico parcial: una cuenta pertenece a una sola persona.
/// </summary>
public sealed record TrabajadorDto(
    Guid Id,
    string NumeroEmpleado,
    string Nombre,
    string? Apellidos,
    Guid PuestoId,
    string Puesto,
    Guid? UbicacionId,
    string? Ubicacion,
    Guid? UsuarioId,
    string? Telefono,
    string? Correo,
    EstadoTrabajador Estado,
    DateOnly? FechaIngreso,
    DateOnly? FechaBaja)
{
    /// <summary>Para listas y documentos, que es donde se lee un nombre completo.</summary>
    public string NombreCompleto =>
        string.IsNullOrWhiteSpace(Apellidos) ? Nombre : $"{Nombre} {Apellidos}";
}

/// <summary>
/// <c>Estado</c> y <c>FechaBaja</c> NO estan aqui: la baja tiene su propia accion porque el
/// CHECK <c>trabajador_baja_coherente</c> exige que el estado Baja y la fecha vayan juntos, y
/// dejar que un PUT los mueva por separado es la forma de topar con ese CHECK como un 500.
/// </summary>
public readonly record struct AltaTrabajador(
    string NumeroEmpleado,
    string Nombre,
    string? Apellidos,
    Guid PuestoId,
    Guid? UbicacionId,
    Guid? UsuarioId,
    string? Telefono,
    string? Correo,
    DateOnly? FechaIngreso);

/// <summary>
/// El cambio de estado, con la fecha que el estado Baja exige.
/// </summary>
/// <param name="FechaBaja">
/// Obligatoria si el estado es Baja, prohibida en cualquier otro: es literalmente lo que dice
/// el CHECK de la base, y aqui se traduce a un mensaje en lugar de a un error del motor.
/// </param>
public readonly record struct CambioEstadoTrabajador(
    EstadoTrabajador Estado,
    DateOnly? FechaBaja);

public sealed record FiltroTrabajadores : Comun.Filtro
{
    public Guid? PuestoId { get; init; }

    public Guid? UbicacionId { get; init; }

    public EstadoTrabajador? Estado { get; init; }
}
