using System.Security.Claims;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Maquinaria.Api.Empresas;

/// <summary>
/// Endpoints que exigen sesion de empresa. Sirven para que el frontend valide un token
/// guardado y sepa que puede mostrar.
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

        return rutas;
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
