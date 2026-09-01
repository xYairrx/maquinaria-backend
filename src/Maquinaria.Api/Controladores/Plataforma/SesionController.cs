using System.Security.Claims;
using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Maquinaria.Api.Controladores.Plataforma;

/// <summary>
/// Acceso al panel de plataforma: lo que usamos nosotros, no los clientes.
/// </summary>
[ApiController]
[Route("api/plataforma/sesion")]
[Tags("Plataforma")]
public sealed class SesionController(IniciarSesionPlataforma iniciarSesion) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasLimitador.InicioSesionPlataforma)]
    [EndpointName("IniciarSesionPlataforma")]
    [EndpointSummary("Inicia sesion como superadministrador de la plataforma.")]
    [ProducesResponseType<SesionPlataforma>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IniciarSesionAsync(
        PeticionInicioSesion peticion, CancellationToken ct)
    {
        // Validacion minima antes de gastar 600 mil iteraciones: un cuerpo vacio no
        // merece el costo de un hash.
        if (string.IsNullOrWhiteSpace(peticion.Correo) || string.IsNullOrEmpty(peticion.Contrasena))
        {
            return Problem(
                title: "Datos incompletos",
                detail: "Correo y contrasena son obligatorios.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.CredencialesObligatorias,
                });
        }

        var sesion = await iniciarSesion.EjecutarAsync(peticion, ct);

        // UN SOLO MENSAJE para las tres causas posibles —el correo no existe, la
        // contrasena no coincide, la cuenta esta inactiva—. Distinguirlas le regalaria
        // a cualquiera la lista de quien tiene acceso a la plataforma.
        return sesion is null
            ? Problem(
                title: "Credenciales incorrectas",
                detail: "Correo o contrasena incorrectos.",
                statusCode: StatusCodes.Status401Unauthorized,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.CredencialesIncorrectas,
                })
            : Ok(sesion.Value);
    }

    /// <summary>
    /// Endpoint PROTEGIDO. Sirve dos cosas: le permite al frontend validar un token
    /// guardado sin adivinar, y es la primera comprobacion de que la politica de ambito
    /// hace su trabajo — un token de empresa no debe entrar aqui.
    /// </summary>
    [HttpGet("actual")]
    [Authorize(PoliticasAutorizacion.Plataforma)]
    [EndpointName("ObtenerSesionActualPlataforma")]
    [EndpointSummary("Devuelve la identidad del superadministrador autenticado.")]
    [ProducesResponseType<IdentidadPlataforma>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult ObtenerSesionActual()
    {
        // Los claims ya los valido JwtBearer: firma, emisor, audiencia y vigencia. Y la
        // politica ya exigio que el ambito sea plataforma. Aqui solo se leen.
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var correo = User.FindFirstValue(JwtRegisteredClaimNames.Email);
        var nombre = User.FindFirstValue(JwtRegisteredClaimNames.Name);

        return id is null || correo is null || nombre is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized)
            : Ok(new IdentidadPlataforma(Guid.Parse(id), correo, nombre));
    }
}

/// <param name="Id">El sub del token.</param>
public readonly record struct IdentidadPlataforma(Guid Id, string Correo, string Nombre);
