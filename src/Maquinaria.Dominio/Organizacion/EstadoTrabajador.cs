namespace Maquinaria.Dominio.Organizacion;

/// <summary>
/// Situacion laboral de un trabajador.
///
/// Mismo criterio que EstadoUsuario: NO SE BORRAN. Un trabajador que se fue sigue
/// apareciendo en las rentas que atendio y en las transferencias que hizo, y borrarlo
/// dejaria ese historial ilegible.
/// </summary>
public enum EstadoTrabajador : short
{
    Activo = 1,

    /// <summary>Incapacidad, permiso, suspension. Reversible.</summary>
    Inactivo = 2,

    /// <summary>Dejo la empresa. No reversible.</summary>
    Baja = 3,
}
