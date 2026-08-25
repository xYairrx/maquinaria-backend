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
    /// <param name="correo">
    /// A quien se pretende invitar. SE IGNORA si la base ya tiene un usuario con acceso
    /// total: en ese caso gana el que ya esta. Por eso hay que mandar la liga al correo
    /// que devuelve este metodo y no al que se paso aqui.
    /// </param>
    Task<AdministradorSembrado> CrearAdministradorAsync(
        string nombreBd, string correo, string nombre, CancellationToken ct);
}

/// <param name="Correo">
/// El correo del administrador REAL de esa base, que puede no ser el que se pidio.
///
/// Se devuelve —en lugar de dar por bueno el de entrada— porque es a donde va la liga de
/// invitacion. Mandarla al correo pedido cuando la base ya tenia otro administrador
/// convertiria un reintento en una forma de tomar esa cuenta: quien lo dispare recibiria
/// una liga que define la contrasena de alguien mas.
/// </param>
/// <param name="TokenEnClaro">Para armar la liga. No se guarda en ninguna parte.</param>
public readonly record struct AdministradorSembrado(string Correo, string TokenEnClaro);
