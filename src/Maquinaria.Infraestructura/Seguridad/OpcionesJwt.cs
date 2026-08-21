namespace Maquinaria.Infraestructura.Seguridad;

/// <summary>
/// Configuracion de los tokens. La llave viene de secretos —user-secrets en
/// desarrollo, variables de entorno en Railway—; el resto es configuracion
/// documentada que se commitea.
/// </summary>
public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";

    /// <summary>Quien emite. Se valida al recibir.</summary>
    public string Emisor { get; set; } = "maquinaria";

    /// <summary>
    /// Audiencia de los tokens de superadministrador.
    ///
    /// SEPARADA de la de empresa a proposito, y esto no es cosmetica: con una sola
    /// audiencia, un token de plataforma serviria en un endpoint de empresa y al
    /// reves, porque los firma la misma llave. Son dos poblaciones de usuarios que
    /// viven en bases distintas y no deben poder suplantarse.
    /// </summary>
    public string AudienciaPlataforma { get; set; } = "maquinaria-plataforma";

    /// <summary>Audiencia de los usuarios de empresa. La usara la rebanada C.</summary>
    public string AudienciaEmpresa { get; set; } = "maquinaria-empresa";

    /// <summary>
    /// Vigencia del token de plataforma.
    ///
    /// Mas larga que la que tendran los de empresa por una razon concreta: la base
    /// central NO tiene tabla sesion_refresh, asi que un superadministrador no puede
    /// renovar y tendria que volver a iniciar sesion. Agregar refresh para plataforma
    /// es una decision de esquema pendiente.
    /// </summary>
    public int MinutosPlataforma { get; set; } = 60;

    /// <summary>
    /// Vigencia del token de empresa. CORTA a proposito: los permisos viajan dentro del
    /// token, asi que esto es lo que acota cuanto tarda en surtir efecto revocar uno.
    /// Se compensa con el refresco rotativo, que la plataforma no tiene.
    /// </summary>
    public int MinutosEmpresa { get; set; } = 15;

    /// <summary>Vigencia del token de refresco.</summary>
    public int DiasRefresco { get; set; } = 30;

    /// <summary>
    /// Debe venir de secretos y tener al menos 32 bytes para HMAC-SHA256. Nunca se
    /// commitea.
    /// </summary>
    public string Llave { get; set; } = string.Empty;
}
