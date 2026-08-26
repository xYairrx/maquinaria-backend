using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Plataforma;

/// <summary>
/// Alta de empresas. Solo la plataforma: aqui se crean bases de datos.
/// </summary>
[ApiController]
[Route("api/plataforma/empresas")]
[Tags("Plataforma")]
[Authorize(PoliticasAutorizacion.Plataforma)]
public sealed class EmpresasController(
    IRegistroTenants registro,
    AprovisionarEmpresa aprovisionar,
    ReenviarInvitacion reenviar) : ControllerBase
{
    [HttpGet]
    [EndpointName("ListarEmpresas")]
    [EndpointSummary("Todas las empresas, con su estado de aprovisionamiento.")]
    [ProducesResponseType<IReadOnlyList<ResumenEmpresa>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(CancellationToken ct)
        => Ok(await registro.ListarAsync(ct));

    [HttpPost]
    [EndpointName("DarDeAltaEmpresa")]
    [EndpointSummary("Da de alta una empresa: crea y migra su base, y invita a su administrador.")]
    [ProducesResponseType<EmpresaAprovisionada>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AltaAsync(AltaDeEmpresa alta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alta.Slug)
            || string.IsNullOrWhiteSpace(alta.RazonSocial)
            || string.IsNullOrWhiteSpace(alta.CorreoAdministrador)
            || string.IsNullOrWhiteSpace(alta.NombreAdministrador))
        {
            return Problem(
                title: "Datos incompletos",
                detail: "Slug, razon social, y correo y nombre del administrador son obligatorios.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var resultado = await aprovisionar.EjecutarAsync(alta, ct);

        if (resultado.Correcto)
        {
            var empresa = resultado.Empresa!.Value;

            return Created($"/api/plataforma/empresas/{empresa.Slug}", empresa);
        }

        // Un rechazo por validacion es 400 y NO es un fallo del sistema: no debe
        // verse igual que un aprovisionamiento roto, ni en la respuesta ni en el log.
        return resultado.EsRechazo
            ? Problem(
                title: "Alta rechazada",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest)
            : Problem(
                title: "Aprovisionamiento incompleto",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Vuelve a correr los pasos 2 a 6 del aprovisionamiento sobre un tenant en Fallida.
    ///
    /// 200 y no 201: el tenant ya existia antes de esta llamada, asi que no se creo
    /// ningun recurso nuevo. El cuerpo es el mismo <see cref="EmpresaAprovisionada"/> del
    /// alta, porque lo que el panel necesita mostrar es lo mismo.
    /// </summary>
    [HttpPost("{slug}/reintento")]
    [EndpointName("ReintentarAltaEmpresa")]
    [EndpointSummary("Reintenta un alta que quedo en Fallida. Solo desde ese estado.")]
    [ProducesResponseType<EmpresaAprovisionada>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ReintentarAsync(
        string slug, ReintentoDeAlta reintento, CancellationToken ct)
    {
        var resultado = await aprovisionar.ReintentarAsync(slug, reintento, ct);

        if (resultado.Correcto)
        {
            return Ok(resultado.Empresa!.Value);
        }

        // ponytail: los tres rechazos —slug inexistente, estado distinto de Fallida y
        // registro inconsistente— salen todos como 400 con su motivo en el detalle, en
        // lugar de 404 y 409 por separado. Es un endpoint del panel de plataforma, ya
        // autenticado, y lo unico que hace la interfaz con la respuesta es mostrar el
        // texto; distinguir codigos no cambiaria una linea del frontend.
        return resultado.EsRechazo
            ? Problem(
                title: "Reintento rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest)
            : Problem(
                title: "Aprovisionamiento incompleto",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Reenvia la invitacion del administrador de una empresa.
    ///
    /// Existe porque el log del alta escribia «Hay que reenviarla» y no habia con que: el
    /// reintento solo acepta empresas en `Fallida`, y una empresa cuya base se creo bien pero
    /// cuyo correo no salio NO esta fallida. La unica salida era borrarla y volver a crearla.
    ///
    /// SIN CUERPO, y eso es la pieza de seguridad. Un campo de correo aqui reabriria la
    /// escalada de privilegios que el reintento del alta tuvo: quien tenga acceso al panel
    /// pediria la liga de una cuenta con acceso total a su propio buzon. El destinatario sale
    /// de la base de la empresa y se DEVUELVE, para que quien lo dispara vea a donde fue en
    /// lugar de preguntarselo.
    ///
    /// UN ENVIO QUE FALLA NO ES UN RECHAZO. Devuelve 200 con `invitacionEnviada: false`,
    /// porque la invitacion SI se reemitio —y la anterior ya quedo invalidada—: lo que falta
    /// es volver a intentar el correo, no repetir la operacion. Contestar un error aqui haria
    /// creer que la liga vieja sigue sirviendo.
    /// </summary>
    [HttpPost("{slug}/invitacion")]
    [EndpointName("ReenviarInvitacionEmpresa")]
    [EndpointSummary("Reemite la invitacion del administrador. Solo si sigue Invitado.")]
    [ProducesResponseType<ResultadoReenvio>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReenviarInvitacionAsync(string slug, CancellationToken ct)
    {
        var resultado = await reenviar.EjecutarAsync(slug, ct);

        // ponytail: los cuatro rechazos —slug mal formado, empresa inexistente,
        // aprovisionamiento a medias y administrador que ya no esta Invitado— salen como 400
        // con su motivo, por lo mismo que el reintento: es un endpoint del panel, ya
        // autenticado, y la interfaz solo muestra el texto.
        return resultado.Correcto
            ? Ok(resultado)
            : Problem(
                title: "Reenvio rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
    }
}
