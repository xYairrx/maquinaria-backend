using System.Net;

namespace Maquinaria.Aplicacion.Comun;

/// <summary>
/// Quien esta actuando en la peticion en curso, para que el interceptor de auditoria
/// pueda escribirlo. De ambito de peticion, igual que <c>IContextoTenant</c> y por la
/// misma razon: lo llena un middleware y lo consume algo que esta muy lejos de la
/// tuberia HTTP.
///
/// NO SE PUDO SACAR DE UN IHttpContextAccessor: Maquinaria.Infraestructura es una
/// biblioteca normal, sin el framework de ASP.NET, y el interceptor vive ahi. El
/// portador tambien es lo que permite que un comando —migrar-empresas— tenga contexto
/// valido sin inventar una peticion falsa: nadie lo establece y queda en 'sistema'.
/// </summary>
public interface IContextoAuditoria
{
    /// <summary>
    /// Agrupa todo lo que se hizo en UNA operacion. Se genera al construir el ambito,
    /// asi que dos SaveChanges de la misma peticion comparten valor — que es justo lo
    /// que <c>Auditoria.CorrelacionId</c> pide, porque el aprovisionamiento guarda mas
    /// de una vez.
    ///
    /// SIEMPRE del lado del servidor. Un X-Correlation-Id del cliente es un id que el
    /// cliente puede repetir para atribuir sus filas al grupo de otra persona.
    /// </summary>
    Guid CorrelacionId { get; }

    /// <summary>
    /// Falso mientras nadie lo haya establecido: no hay peticion HTTP detras. Es lo
    /// que distingue 'sistema' de los demas origenes.
    /// </summary>
    bool EstaEstablecido { get; }

    Guid? UsuarioId { get; }

    string? UsuarioCorreo { get; }

    /// <summary>
    /// Los codigos de los roles efectivos, salidos del JWT. Vacio, nunca null.
    /// </summary>
    string[] Roles { get; }

    /// <summary>
    /// Si el token es de ambito plataforma. NO es el origen: el origen lo decide el
    /// interceptor, porque depende de contra que base se esta escribiendo.
    /// </summary>
    bool EsPlataforma { get; }

    IPAddress? Ip { get; }

    /// <summary>Lo llama el middleware. Una sola vez por peticion.</summary>
    void Establecer(
        Guid? usuarioId, string? correo, string[] roles, bool esPlataforma, IPAddress? ip);
}
