using System.Net;
using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Infraestructura.Servicios.Comun;

/// <summary>
/// Portador de ambito de peticion del actor en curso. Lo llena el middleware.
///
/// A diferencia de <c>ContextoTenant</c>, este NO LANZA cuando nadie lo establecio:
/// un comando de mantenimiento no tiene peticion y sus escrituras se auditan como
/// 'sistema'. Exigir un actor aqui volveria inarrancable migrar-empresas.
/// </summary>
internal sealed class ContextoAuditoria : IContextoAuditoria
{
    public Guid CorrelacionId { get; } = Guid.CreateVersion7();

    public bool EstaEstablecido { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public string? UsuarioCorreo { get; private set; }

    public string[] Roles { get; private set; } = [];

    public bool EsPlataforma { get; private set; }

    public IPAddress? Ip { get; private set; }

    public void Establecer(
        Guid? usuarioId, string? correo, string[] roles, bool esPlataforma, IPAddress? ip)
    {
        if (EstaEstablecido)
        {
            // Reasignar significaria que una peticion cambio de actor a medio camino.
            // Mismo criterio que ContextoTenant: es un error grave y silencioso si se
            // permite, y en una bitacora ademas atribuiria filas a quien no fue.
            throw new InvalidOperationException(
                "El actor de esta peticion ya estaba establecido y no se puede cambiar.");
        }

        EstaEstablecido = true;
        UsuarioId = usuarioId;
        UsuarioCorreo = correo;
        Roles = roles;
        EsPlataforma = esPlataforma;
        Ip = ip;
    }
}
