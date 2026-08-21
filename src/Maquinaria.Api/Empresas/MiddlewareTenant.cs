using System.Security.Claims;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Infraestructura.Seguridad;

namespace Maquinaria.Api.Empresas;

/// <summary>
/// Resuelve la empresa de la peticion a partir del JWT y la deja en
/// <see cref="IContextoTenant"/>.
///
/// Corre DESPUES de la autenticacion —necesita los claims ya validados— y ANTES de
/// cualquier cosa que abra la base de una empresa.
///
/// NO rechaza las peticiones sin tenant. Las anonimas y las de plataforma no
/// pertenecen a ninguna empresa y son perfectamente validas; simplemente pasan sin
/// resolver nada, y si un endpoint intenta abrir un ContextoEmpresa sin tenant, el
/// propio contexto revienta. Ese es el orden correcto de las responsabilidades:
/// el middleware resuelve, el contexto exige.
/// </summary>
internal sealed class MiddlewareTenant(RequestDelegate siguiente, ILogger<MiddlewareTenant> log)
{
    /// <summary>Claim que lleva el id del tenant en los tokens de empresa.</summary>
    public const string ClaimTenant = "tenant";

    public async Task InvokeAsync(
        HttpContext contexto, IContextoTenant contextoTenant, IDirectorioTenants directorio)
    {
        var claim = contexto.User.FindFirstValue(ClaimTenant);

        if (claim is null)
        {
            // SIN JWT PERO CON SLUG EN LA RUTA: son los flujos anonimos por empresa
            // —aceptar una invitacion, iniciar sesion—. Hay que resolver aqui porque el
            // contenedor construye ContextoEmpresa al inyectar el caso de uso, o sea
            // ANTES de que el caso de uso pudiera establecer nada. Establecerlo dentro
            // del caso de uso llegaba tarde: es el error que este comentario documenta.
            await ResolverPorRutaAsync(contexto, contextoTenant, directorio);

            await siguiente(contexto);
            return;
        }

        // Un ambito de plataforma con claim de tenant no tiene sentido y solo puede
        // venir de un token mal emitido. Se corta aqui en lugar de dejarlo pasar a
        // medias.
        var ambito = contexto.User.FindFirstValue(ProveedorTokensJwt.ClaimAmbito);

        if (ambito != ProveedorTokensJwt.AmbitoEmpresa)
        {
            log.LogWarning(
                "Token con claim de tenant pero ambito {Ambito}. Se rechaza.", ambito);

            await Rechazar(contexto, "El token no corresponde a una empresa.");
            return;
        }

        if (!Guid.TryParse(claim, out var tenantId))
        {
            await Rechazar(contexto, "El token no identifica una empresa valida.");
            return;
        }

        var tenant = await directorio.BuscarPorIdAsync(tenantId, contexto.RequestAborted);

        if (tenant is null)
        {
            // El tenant existia cuando se emitio el token y ya no. Un token valido
            // sobre una empresa dada de baja no debe seguir abriendo su base.
            log.LogWarning("Token de un tenant que ya no existe: {TenantId}.", tenantId);

            await Rechazar(contexto, "La empresa ya no esta disponible.");
            return;
        }

        if (!tenant.PuedeOperar)
        {
            // Cubre suspendida, cancelada y base a medio aprovisionar. Se comprueba en
            // CADA peticion y no solo en el login: suspender a un cliente tiene que
            // surtir efecto sin esperar a que caduquen los tokens que ya emitio.
            log.LogInformation(
                "Peticion de {Slug} rechazada: estado {Estado}, aprovisionamiento {Aprov}.",
                tenant.Slug, tenant.Estado, tenant.Aprovisionamiento);

            await Rechazar(contexto, "La empresa no puede operar en este momento.");
            return;
        }

        contextoTenant.Establecer(tenant);

        // El slug entra al ambito de logging: sin esto, un log de produccion con varias
        // empresas es imposible de leer.
        using (log.BeginScope("{Empresa}", tenant.Slug))
        {
            await siguiente(contexto);
        }
    }

    /// <summary>
    /// Resuelve por el slug de la ruta y, si no se puede, NO RECHAZA: sigue sin tenant.
    ///
    /// Esa diferencia con el camino del JWT es deliberada. Rechazar aqui con un 403
    /// "empresa no disponible" delataria que slugs son clientes y cuales no, que es
    /// justo lo que las tres reglas anti-filtracion del login evitan. El caso de uso
    /// responde con su mensaje uniforme y en tiempo constante, y para eso comprueba
    /// IContextoTenant.EstaResuelto.
    /// </summary>
    private static async Task ResolverPorRutaAsync(
        HttpContext contexto, IContextoTenant contextoTenant, IDirectorioTenants directorio)
    {
        if (contexto.Request.RouteValues["slug"] is not string slug || slug.Length == 0)
        {
            return;
        }

        var tenant = await directorio.BuscarPorSlugAsync(slug, contexto.RequestAborted);

        if (tenant is not null && tenant.PuedeOperar && !contextoTenant.EstaResuelto)
        {
            contextoTenant.Establecer(tenant);
        }
    }

    private static async Task Rechazar(HttpContext contexto, string detalle)
    {
        contexto.Response.StatusCode = StatusCodes.Status403Forbidden;

        await contexto.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            title = "Empresa no disponible",
            status = StatusCodes.Status403Forbidden,
            detail = detalle,
        });
    }
}

internal static class ExtensionesMiddlewareTenant
{
    public static IApplicationBuilder UsarResolucionDeTenant(this IApplicationBuilder app)
        => app.UseMiddleware<MiddlewareTenant>();
}
