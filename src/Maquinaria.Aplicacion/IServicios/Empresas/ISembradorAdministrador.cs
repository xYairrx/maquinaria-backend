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

    /// <summary>
    /// Vuelve a emitir la invitacion del administrador que YA existe en esa base, e
    /// invalida la anterior.
    ///
    /// NO RECIBE CORREO, y esa ausencia es el punto entero del metodo. El destinatario
    /// sale de la base. Un parametro de correo aqui seria la misma puerta que el reintento
    /// del alta tuvo abierta: quien tenga acceso al panel pediria la liga de una cuenta con
    /// acceso total a su propio buzon y definiria su contrasena.
    ///
    /// Existe porque el log del alta decia «hay que reenviarla» y no habia nada que lo
    /// hiciera. Con el correo caido un rato, la unica salida era borrar la empresa y
    /// volver a crearla.
    /// </summary>
    Task<ResultadoReemision> ReemitirInvitacionAsync(string nombreBd, CancellationToken ct);
}

/// <summary>
/// El resultado de reemitir. Rechaza en lugar de lanzar porque los tres motivos son
/// situaciones normales que la interfaz tiene que poder explicar, no fallos.
/// </summary>
public readonly record struct ResultadoReemision(
    bool Correcto, string? Motivo, string Correo, string TokenEnClaro)
{
    public static ResultadoReemision Exito(string correo, string token) =>
        new(true, null, correo, token);

    public static ResultadoReemision Rechazado(string motivo) =>
        new(false, motivo, string.Empty, string.Empty);
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
