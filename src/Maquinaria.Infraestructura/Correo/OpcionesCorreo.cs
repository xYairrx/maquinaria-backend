namespace Maquinaria.Infraestructura.Correo;

public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    /// <summary>
    /// "log" o "resend". Mismo criterio que IAlmacenamientoArchivos: en desarrollo la
    /// implementacion de log; en la nube, el proveedor real.
    /// </summary>
    public string Proveedor { get; set; } = "log";

    /// <summary>
    /// Remitente. TIENE QUE SER DE UN DOMINIO VERIFICADO EN RESEND.
    ///
    /// Mientras el dominio no este verificado, Resend solo acepta onboarding@resend.dev
    /// y solo entrega al correo del titular de la cuenta. Es una limitacion del sandbox,
    /// no un error de configuracion.
    /// </summary>
    public string Remitente { get; set; } = "onboarding@resend.dev";

    public string NombreRemitente { get; set; } = "Maquinaria";

    /// <summary>Base de las ligas del correo. La URL publica del frontend.</summary>
    public string UrlBaseAplicacion { get; set; } = "http://localhost:4200";

    /// <summary>
    /// Si el alta devuelve la liga de invitacion en la respuesta HTTP.
    ///
    /// SOLO en desarrollo, y por eso arranca en false: en produccion cualquiera con
    /// acceso al panel podria tomar la sesion del administrador de un cliente antes de
    /// que ese abra su correo.
    /// </summary>
    public bool DevolverLigaEnRespuesta { get; set; }

    /// <summary>Dias que dura una invitacion. Un restablecimiento durara una hora.</summary>
    public int DiasVigenciaInvitacion { get; set; } = 7;
}

public sealed class OpcionesResend
{
    public const string Seccion = "Resend";

    /// <summary>Va en secretos. Nunca se commitea.</summary>
    public string Llave { get; set; } = string.Empty;

    public string UrlBase { get; set; } = "https://api.resend.com";

    /// <summary>
    /// Corto a proposito: el envio es best-effort y no debe alargar el alta de una
    /// empresa si Resend esta lento.
    /// </summary>
    public int SegundosTimeout { get; set; } = 10;
}
