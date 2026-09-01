using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Maquinaria.Api.Controladores.Empresas;

/// <summary>
/// Restablecimiento de contrasena: pedir la liga, comprobarla y canjearla.
/// </summary>
[ApiController]
[Route("api/empresas/{slug}/restablecimientos")]
[Tags("Empresa")]
[AllowAnonymous]
[EnableRateLimiting(PoliticasLimitador.AccesoEmpresa)]
public sealed class RestablecimientosController(
    SolicitarRestablecimiento solicitar,
    Restablecimientos restablecimientos) : ControllerBase
{
    /// <summary>
    /// UNA SOLA SALIDA, y por eso no hay ni un if aqui.
    ///
    /// El caso de uso no devuelve nada —no se puede ramificar sobre lo que no existe— y
    /// el cuerpo es una instancia unica y estatica, no un objeto armado por peticion: no
    /// hay forma de que se cuele una diferencia de contenido, de orden de propiedades ni
    /// de longitud entre "el correo existe" y "el correo no existe". El tiempo lo iguala
    /// el propio caso de uso.
    ///
    /// 202 y no 200: lo unico que se puede afirmar es que la peticion se acepto. Si el
    /// correo salio, a quien salio y si habia cuenta detras son exactamente los datos que
    /// no se pueden confirmar.
    /// </summary>
    [HttpPost]
    // SUSTITUYE a la politica del controlador, no se suma: el middleware del limitador lee
    // UNA politica de la metadata del endpoint, y la de la accion se agrega despues de la
    // de la clase, asi que gana. Es lo que se quiere: la mas estricta. Es el mismo
    // mecanismo que tenia el grupo de Minimal API, con otra sintaxis.
    [EnableRateLimiting(PoliticasLimitador.RestablecimientoEmpresa)]
    [EndpointName("SolicitarRestablecimiento")]
    [EndpointSummary("Pide una liga para restablecer la contrasena.")]
    [ProducesResponseType<RestablecimientoSolicitado>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> SolicitarAsync(
        string slug, PeticionRestablecimiento peticion, CancellationToken ct)
    {
        await solicitar.EjecutarAsync(slug, peticion, ct);

        return StatusCode(StatusCodes.Status202Accepted, RespuestaUnica);
    }

    /// <summary>
    /// Se construye una vez y se reusa. Ver el comentario de arriba: la respuesta tiene
    /// que ser identica byte a byte en los dos casos.
    /// </summary>
    private static readonly RestablecimientoSolicitado RespuestaUnica = new(
        "Si el correo corresponde a una cuenta activa, ahi llegara la liga para "
        + "restablecer la contrasena. Caduca en una hora.");

    [HttpGet("{token}")]
    [EndpointName("ConsultarRestablecimiento")]
    [EndpointSummary("Dice si una liga de restablecimiento todavia sirve.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConsultarAsync(string slug, string token, CancellationToken ct)
    {
        var usable = await restablecimientos.EsUsableAsync(slug, token, ct);

        // 204 sin cuerpo, no un objeto con los datos de la cuenta como en la invitacion.
        // Aqui la pantalla solo necesita saber si tiene sentido pedir la contrasena, y
        // cualquier dato de mas convierte una liga adivinada en una fuente de informacion.
        return usable
            ? NoContent()
            : Problem(
                title: "Liga no valida",
                detail: "La liga no existe, ya se uso o caduco.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.LigaNoValida,
                });
    }

    [HttpPost("{token}")]
    [EndpointName("Restablecer")]
    [EndpointSummary("Define la contrasena nueva y cierra las sesiones abiertas.")]
    [ProducesResponseType<AceptacionAceptada>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RestablecerAsync(
        string slug, string token, DefinirContrasena cuerpo, CancellationToken ct)
    {
        var resultado = await restablecimientos.RestablecerAsync(slug, token, cuerpo.Contrasena, ct);

        return resultado.Correcto
            ? Ok(new AceptacionAceptada(resultado.Correo!, slug))
            : Problem(
                title: "No se pudo restablecer la contrasena",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
    }
}

/// <param name="Mensaje">
/// El mismo texto siempre. Esta redactado para ser cierto en los dos casos: no afirma
/// que se haya mandado nada, y le dice a quien si tiene cuenta que revise su buzon.
/// </param>
public readonly record struct RestablecimientoSolicitado(string Mensaje);
