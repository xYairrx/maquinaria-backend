using System.Security.Claims;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Maquinaria.Api.Empresas;

/// <summary>
/// El ciclo de vida de una sesion de empresa ya iniciada: consultarla y renovarla.
/// </summary>
internal static class EndpointsSesionEmpresa
{
    public static IEndpointRouteBuilder MapearSesionEmpresa(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/mi")
            .WithTags("Empresa")
            .RequireAuthorization(PoliticasAutorizacion.Empresa);

        grupo.MapGet("/sesion", ObtenerAsync)
            .WithName("ObtenerSesionEmpresa")
            .WithSummary("Identidad, empresa y permisos efectivos del usuario autenticado.")
            .Produces<IdentidadEmpresa>();

        // ------------------------------------------------------------ refresco --
        // GRUPO APARTE, y las tres diferencias con el de arriba son deliberadas:
        //
        // 1. ANONIMO. Se refresca precisamente porque el token de acceso ya caduco, asi
        //    que exigir uno valido haria el endpoint inutil. Lo que autentica aqui es el
        //    token de refresco, y eso lo comprueba el caso de uso.
        // 2. EL SLUG VA EN LA RUTA, igual que en el login. Es lo que hace que
        //    MiddlewareTenant resuelva la empresa —sin claim de tenant, resuelve por
        //    ruta— y por tanto lo que garantiza que la sesion se busque en la base de ESA
        //    empresa y no en otra. Sin slug no hay tenant y el caso de uso rechaza.
        // 3. LIMITE DE INTENTOS por slug e IP, la misma politica del acceso de empresa:
        //    un token de refresco es un secreto de 256 bits que no se adivina, pero el
        //    endpoint es anonimo y escribe en la base.
        var refresco = rutas.MapGroup("/api/empresas/{slug}")
            .WithTags("Empresa")
            .AllowAnonymous()
            .RequireRateLimiting(EndpointsEmpresa.PoliticaAcceso);

        refresco.MapPost("/sesion/refresco", RefrescarAsync)
            .WithName("RefrescarSesionEmpresa")
            .WithSummary("Canjea el token de refresco por una sesion nueva y rota el anterior.")
            .Produces<SesionEmpresa>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return rutas;
    }

    /// <summary>
    /// Devuelve EXACTAMENTE la misma forma que el login —<see cref="SesionEmpresa"/>—
    /// para que el frontend tenga un solo contrato de sesion y su interceptor pueda
    /// sustituir lo que tenia guardado sin traducir nada.
    ///
    /// Y un solo 401 para todos los motivos: token inexistente, caducado, revocado,
    /// reusado, usuario que ya no esta activo o empresa que no puede operar. Distinguirlos
    /// le diria a quien prueba tokens y slugs cuales existen.
    /// </summary>
    private static async Task<IResult> RefrescarAsync(
        string slug, PeticionRefresco peticion, HttpContext contexto,
        IniciarSesionEmpresa caso, CancellationToken ct)
    {
        var sesion = await caso.RefrescarAsync(
            slug,
            peticion,
            contexto.Connection.RemoteIpAddress?.ToString(),
            contexto.Request.Headers.UserAgent.ToString(),
            ct);

        return sesion is null
            ? Results.Problem(
                title: "Sesion no valida",
                detail: IniciarSesionEmpresa.MotivoRefrescoUniforme,
                statusCode: StatusCodes.Status401Unauthorized)
            : Results.Ok(sesion.Value);
    }

    private static IResult ObtenerAsync(ClaimsPrincipal quien, IContextoTenant tenant)
    {
        // Los permisos se leen del TOKEN, no de la base: es justo lo que se compro
        // metiendolos dentro. Y el tenant lo resolvio el middleware, asi que llegar aqui
        // ya prueba que la empresa puede operar.
        var accesoTotal = quien.HasClaim(ProveedorTokensJwt.ClaimAccesoTotal, "true");

        var permisos = quien.FindFirst(ProveedorTokensJwt.ClaimPermisos)?.Value
            ?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

        return Results.Ok(new IdentidadEmpresa(
            quien.FindFirstValue(JwtRegisteredClaimNames.Email) ?? "",
            quien.FindFirstValue(JwtRegisteredClaimNames.Name) ?? "",
            tenant.Actual.Slug,
            tenant.Actual.RazonSocial,
            accesoTotal,
            permisos,
            [.. tenant.Actual.Modulos]));
    }
}

/// <param name="Modulos">Lo que el plan incluye. La interfaz oculta lo que no esta.</param>
public readonly record struct IdentidadEmpresa(
    string Correo,
    string Nombre,
    string Empresa,
    string RazonSocial,
    bool AccesoTotal,
    string[] Permisos,
    string[] Modulos);
