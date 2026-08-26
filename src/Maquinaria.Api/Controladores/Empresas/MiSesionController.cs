using System.Security.Claims;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Api.Seguridad;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Maquinaria.Api.Controladores.Empresas;

/// <summary>
/// Lo que el usuario autenticado sabe de si mismo. Cuelga de /api/mi y no de
/// /api/empresas/{slug} a proposito: aqui la empresa sale del TOKEN, no de la ruta, asi
/// que no hay forma de pedir la sesion de otra.
/// </summary>
[ApiController]
[Route("api/mi")]
[Tags("Empresa")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class MiSesionController(IContextoTenant tenant) : ControllerBase
{
    [HttpGet("sesion")]
    [EndpointName("ObtenerSesionEmpresa")]
    [EndpointSummary("Identidad, empresa y permisos efectivos del usuario autenticado.")]
    [ProducesResponseType<IdentidadEmpresa>(StatusCodes.Status200OK)]
    public IActionResult Obtener()
    {
        // Los permisos se leen del TOKEN, no de la base: es justo lo que se compro
        // metiendolos dentro. Y el tenant lo resolvio el middleware, asi que llegar aqui
        // ya prueba que la empresa puede operar.
        var accesoTotal = User.HasClaim(ProveedorTokensJwt.ClaimAccesoTotal, "true");

        var permisos = User.FindFirst(ProveedorTokensJwt.ClaimPermisos)?.Value
            ?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

        return Ok(new IdentidadEmpresa(
            User.FindFirstValue(JwtRegisteredClaimNames.Email) ?? "",
            User.FindFirstValue(JwtRegisteredClaimNames.Name) ?? "",
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
