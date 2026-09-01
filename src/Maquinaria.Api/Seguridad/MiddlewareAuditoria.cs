using System.Security.Claims;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Maquinaria.Api.Seguridad;

/// <summary>
/// Deja en <see cref="IContextoAuditoria"/> quien esta actuando, para que el
/// interceptor de auditoria lo escriba sin conocer la tuberia HTTP.
///
/// Corre DESPUES de la autenticacion —necesita los claims validados— y ANTES de la
/// resolucion de tenant, porque el login de empresa ya escribe una fila de
/// sesion_refresh en ese camino.
///
/// NO RECHAZA NADA, nunca. Una peticion anonima es un actor valido: es la que inicia
/// sesion, la que acepta una invitacion y la que pide un restablecimiento, y las tres
/// escriben. Queda con usuario nulo y roles vacios, que es exactamente lo que
/// significa.
/// </summary>
internal sealed class MiddlewareAuditoria(RequestDelegate siguiente)
{
    public async Task InvokeAsync(HttpContext contexto, IContextoAuditoria actor)
    {
        var usuario = contexto.User;

        // Guid.TryParse sobre el claim y no Guid.Parse: el token esta firmado, pero un
        // sub ilegible tiene que dejar la peticion sin usuario, no tumbarla con un 500
        // desde un middleware que solo esta anotando.
        Guid? usuarioId =
            Guid.TryParse(usuario.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
                ? id
                : null;

        var esPlataforma = usuario.FindFirstValue(ProveedorTokensJwt.ClaimAmbito)
            == ProveedorTokensJwt.AmbitoPlataforma;

        actor.Establecer(
            usuarioId,
            usuario.FindFirstValue(JwtRegisteredClaimNames.Email),
            LeerRoles(usuario),
            esPlataforma,

            // La IP de la conexion, no la de X-Forwarded-For: detras de Railway hay un
            // proxy, asi que esto registra la del proxy hasta que se configure
            // UseForwardedHeaders. Una cabecera que el cliente controla no puede ser la
            // fuente de un dato de auditoria sin que algo la valide antes.
            contexto.Connection.RemoteIpAddress);

        await siguiente(contexto);
    }

    /// <summary>
    /// Los roles salen del JWT y no de la base: son los que autorizaron ESTA peticion,
    /// congelados al emitir el token igual que los permisos. Consultarlos aqui costaria
    /// una consulta por peticion y ademas registraria los roles de AHORA, que pueden no
    /// ser los que dejaron pasar la accion.
    ///
    /// Arreglo vacio y no null cuando el claim no viene —tokens de plataforma, peticiones
    /// anonimas—: vacio afirma "ningun rol", null seria "no se sabe".
    /// </summary>
    private static string[] LeerRoles(ClaimsPrincipal usuario)
    {
        var claim = usuario.FindFirstValue(ProveedorTokensJwt.ClaimRoles);

        return string.IsNullOrEmpty(claim)
            ? []
            : claim.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}

internal static class ExtensionesMiddlewareAuditoria
{
    public static IApplicationBuilder UsarContextoDeAuditoria(this IApplicationBuilder app)
        => app.UseMiddleware<MiddlewareAuditoria>();
}
