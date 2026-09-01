using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Maquinaria.Api.Controladores.Empresas;

/// <summary>
/// La liga de invitacion: consultarla y canjearla por una cuenta activa.
///
/// EL SLUG VA EN LA RUTA Y NO EN EL CUERPO, y no es estetica: el limitador de intentos de
/// .NET corre ANTES de leer el cuerpo de la peticion, asi que con el slug en el cuerpo era
/// imposible particionar por empresa. En la ruta si.
/// </summary>
[ApiController]
[Route("api/empresas/{slug}/invitaciones")]
[Tags("Empresa")]
[AllowAnonymous]
[EnableRateLimiting(PoliticasLimitador.AccesoEmpresa)]
public sealed class InvitacionesController(Invitaciones invitaciones) : ControllerBase
{
    [HttpGet("{token}")]
    [EndpointName("ConsultarInvitacion")]
    [EndpointSummary("Dice a quien va dirigida una liga de invitacion vigente.")]
    [ProducesResponseType<InvitacionVigente>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarAsync(string slug, string token, CancellationToken ct)
    {
        var invitacion = await invitaciones.ConsultarAsync(slug, token, ct);

        // 404 para TODOS los motivos: empresa inexistente, token invalido, usado,
        // invalidado o caducado. Distinguirlos le diria a cualquiera con una liga vieja
        // si la cuenta existe y en que estado esta.
        return invitacion is null
            ? Problem(
                title: "Liga no valida",
                detail: "La liga no existe, ya se uso o caduco.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.LigaNoValida,
                })
            : Ok(invitacion.Value);
    }

    [HttpPost("{token}")]
    [EndpointName("AceptarInvitacion")]
    [EndpointSummary("Define la contrasena y activa la cuenta.")]
    [ProducesResponseType<AceptacionAceptada>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AceptarAsync(
        string slug, string token, DefinirContrasena cuerpo, CancellationToken ct)
    {
        var resultado = await invitaciones.AceptarAsync(slug, token, cuerpo.Contrasena, ct);

        return resultado.Correcto
            ? Ok(new AceptacionAceptada(resultado.Correo!, slug))
            : Problem(
                title: "No se pudo activar la cuenta",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
    }
}

public readonly record struct DefinirContrasena(string Contrasena);

public readonly record struct AceptacionAceptada(string Correo, string Empresa);
