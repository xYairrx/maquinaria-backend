using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Maquinaria.Api.Controladores.Empresas;

/// <summary>
/// Abrir y renovar la sesion de un usuario de empresa.
///
/// LAS DOS ACCIONES SON ANONIMAS, y en el refresco eso es deliberado y no un descuido: se
/// refresca precisamente porque el token de acceso ya caduco, asi que exigir uno valido
/// haria el endpoint inutil. Lo que autentica ahi es el token de refresco, y eso lo
/// comprueba el caso de uso.
///
/// EL SLUG VA EN LA RUTA en las dos. Es lo que hace que MiddlewareTenant resuelva la
/// empresa —sin claim de tenant, resuelve por ruta— y por tanto lo que garantiza que la
/// sesion se busque en la base de ESA empresa y no en otra. Sin slug no hay tenant y el
/// caso de uso rechaza.
///
/// Y LIMITE DE INTENTOS por slug e IP en las dos: un token de refresco es un secreto de
/// 256 bits que no se adivina, pero el endpoint es anonimo y escribe en la base.
/// </summary>
[ApiController]
[Route("api/empresas/{slug}/sesion")]
[Tags("Empresa")]
[AllowAnonymous]
[EnableRateLimiting(PoliticasLimitador.AccesoEmpresa)]
public sealed class SesionEmpresaController(IniciarSesionEmpresa iniciarSesion) : ControllerBase
{
    [HttpPost]
    [EndpointName("IniciarSesionEmpresa")]
    [EndpointSummary("Inicia sesion con empresa, correo y contrasena.")]
    [ProducesResponseType<SesionEmpresa>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> IniciarAsync(
        string slug, PeticionSesionEmpresa peticion, CancellationToken ct)
    {
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

        var resultado = await iniciarSesion.EjecutarAsync(
            slug,
            peticion,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            ct);

        if (resultado.Sesion is not null)
        {
            return Ok(resultado.Sesion.Value);
        }

        // SERVICIO DETENIDO: 403 y no 401, y las credenciales ERAN correctas.
        //
        // El codigo importa del lado del cliente: un 401 dispara el interceptor de
        // refresco, que intentaria renovar una sesion que no existe. Un 403 dice lo que
        // pasa —te identificamos, y aun asi no entras—.
        if (resultado.ServicioDetenido is { } estado)
        {
            var cancelado = estado == EstadoTenant.Cancelado;

            return Problem(
                title: cancelado ? "Servicio cancelado" : "Servicio suspendido",
                detail: cancelado
                    ? "El servicio de esta empresa esta cancelado. Ponte en contacto con "
                        + "quien administra la cuenta."
                    : "El servicio de esta empresa esta suspendido. Ponte en contacto con "
                        + "quien administra la cuenta.",
                statusCode: StatusCodes.Status403Forbidden,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = cancelado
                        ? CodigosProblema.ServicioCancelado
                        : CodigosProblema.ServicioSuspendido,
                });
        }

        // UN SOLO MENSAJE para las demas causas: la empresa no existe, su base no esta
        // lista, el correo no existe, el usuario no esta activo, o la contrasena no
        // coincide. Distinguirlas le regalaria a cualquiera la lista de clientes.
        return Problem(
            title: "Credenciales incorrectas",
            detail: "Empresa, correo o contrasena incorrectos.",
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: new Dictionary<string, object?>
            {
                // UNO SOLO para las cinco causas. Un codigo por causa seria el enumerador
                // de clientes que el mensaje uniforme evita.
                ["codigo"] = CodigosProblema.CredencialesIncorrectas,
            });
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
    [HttpPost("refresco")]
    [EndpointName("RefrescarSesionEmpresa")]
    [EndpointSummary("Canjea el token de refresco por una sesion nueva y rota el anterior.")]
    [ProducesResponseType<SesionEmpresa>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefrescarAsync(
        string slug, PeticionRefresco peticion, CancellationToken ct)
    {
        var sesion = await iniciarSesion.RefrescarAsync(
            slug,
            peticion,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            HttpContext.Request.Headers.UserAgent.ToString(),
            ct);

        return sesion is null
            ? Problem(
                title: "Sesion no valida",
                detail: IniciarSesionEmpresa.MotivoRefrescoUniforme,
                statusCode: StatusCodes.Status401Unauthorized)
            : Ok(sesion.Value);
    }
}
