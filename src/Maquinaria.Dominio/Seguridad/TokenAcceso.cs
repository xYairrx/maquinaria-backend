using System.Linq.Expressions;

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

    /// <summary>
    /// QUE SIGNIFICA QUE UN TOKEN SIRVA, escrito UNA SOLA VEZ: del proposito pedido,
    /// sin usar, sin invalidar y sin caducar.
    ///
    /// Es una Expression y no un metodo normal a proposito. Un metodo no se puede
    /// traducir a SQL —EF Core solo entiende arboles de expresion— asi que la regla
    /// habria acabado escrita dos veces: una en la consulta de infraestructura y otra
    /// en cualquier prueba que quiera comprobarla sin base de datos. Dos copias de una
    /// regla de seguridad es una copia que se queda atras.
    ///
    /// El proposito va DENTRO de la condicion, no como filtro aparte, porque es
    /// justamente la parte que un copy-paste del flujo de invitacion olvidaria: sin
    /// el, un token emitido para invitar serviria para restablecer.
    /// </summary>
    public static Expression<Func<TokenAcceso, bool>> Vigente(
        PropositoToken proposito, DateTime ahoraUtc)
        => t => t.Proposito == proposito
            && t.UsadoEn == null
            && t.InvalidadoEn == null
            && t.ExpiraEn > ahoraUtc;
}
