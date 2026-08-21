namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Liga de un solo uso, con vigencia. Una tabla para dos propositos —invitar y
/// restablecer— porque son el mismo mecanismo y solo cambia la intencion; dos
/// tablas serian duplicacion.
///
/// POR QUE NO MANDAR UNA CONTRASENA TEMPORAL POR CORREO: viajaria en texto plano y
/// se quedaria en la bandeja del destinatario para siempre. Con el token, la
/// contrasena NUNCA VIAJA: la persona recibe una liga, la abre, y la define ella.
/// </summary>
public class TokenAcceso
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UsuarioId { get; set; }

    public PropositoToken Proposito { get; set; }

    /// <summary>
    /// Se guarda el HASH, no el token. Leer la base no debe dar ligas usables.
    /// Mismo criterio que las contrasenas. UNIQUE.
    /// </summary>
    public required string HashToken { get; set; }

    /// <summary>
    /// Va en la fila y no en configuracion porque depende del proposito: una
    /// invitacion puede durar dias, un restablecimiento debe durar una hora.
    /// </summary>
    public DateTime ExpiraEn { get; set; }

    /// <summary>
    /// Lo vuelve de un solo uso: un token ya usado se rechaza aunque no haya
    /// caducado.
    /// </summary>
    public DateTime? UsadoEn { get; set; }

    /// <summary>
    /// Al reenviar una invitacion se invalida la anterior, para que no queden dos
    /// ligas validas circulando.
    /// </summary>
    public DateTime? InvalidadoEn { get; set; }

    /// <summary>
    /// NULLABLE porque una invitacion la puede crear el administrador de la
    /// empresa —que esta en usuario— o un superadministrador nuestro, que vive en
    /// la base central y NO EXISTE AQUI. NULL significa "la creo la plataforma".
    /// </summary>
    public Guid? CreadoPorId { get; set; }

    public DateTime CreadoEn { get; set; }

    public Usuario? Usuario { get; set; }
}
