namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Crea el primer usuario de una empresa recien aprovisionada, dentro de SU base.
/// </summary>
public interface ISembradorAdministrador
{
    /// <summary>
    /// Crea el usuario en estado Invitado, le asigna el rol 'administrador' y emite su
    /// token de invitacion.
    ///
    /// Es IDEMPOTENTE: si el usuario ya existe —porque un alta anterior fallo despues
    /// de este paso— no lo duplica; invalida las invitaciones pendientes y emite una
    /// nueva. Reintentar un alta no debe dejar dos ligas validas circulando.
    ///
    /// ESTE ES EL UNICO LUGAR donde se asigna el rol 'administrador'. No aparece en la
    /// interfaz de asignaciones, asi que la empresa tendra exactamente una persona con
    /// acceso total.
    /// </summary>
    /// <returns>El token EN CLARO, para armar la liga. No se guarda en ninguna parte.</returns>
    Task<string> CrearAdministradorAsync(
        string nombreBd, string correo, string nombre, CancellationToken ct);
}
