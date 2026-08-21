namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Para que sirve un <see cref="TokenAcceso"/>.
///
/// Existe para impedir que un token emitido para restablecer la contrasena sirva
/// para aceptar una invitacion, o al reves. Sin este campo, una tabla para dos
/// propositos seria una tabla con dos agujeros.
/// </summary>
public enum PropositoToken : short
{
    /// <summary>Alta de un usuario nuevo. Vigencia larga: dias.</summary>
    Invitacion = 1,

    /// <summary>Recuperacion de una cuenta existente. Vigencia corta: una hora.</summary>
    RestablecerContrasena = 2,
}
