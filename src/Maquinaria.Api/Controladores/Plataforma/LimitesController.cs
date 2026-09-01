using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Plataforma;

/// <summary>
/// Los cupos de UNA empresa. Cuelga de la empresa y no de un recurso /limites suelto,
/// porque un cupo sin empresa no existe: la tabla del catalogo —los tipos— es otra cosa
/// y no se administra desde aqui.
///
/// CONTROLADOR APARTE Y NO ACCIONES DE EmpresasController, por lo mismo que
/// ModulosController vive fuera de PlanesController: la ruta base va en la clase, y
/// `empresas/{slug}/limites` es otro recurso. EmpresasController administra el alta y el
/// ciclo de aprovisionamiento; esto administra un dato de negociacion comercial.
/// </summary>
[ApiController]
[Route("api/plataforma/empresas/{slug}/limites")]
[Tags("Plataforma")]
[Authorize(PoliticasAutorizacion.Plataforma)]
public sealed class LimitesController(ILimitesTenant limites) : ControllerBase
{
    [HttpGet]
    [EndpointName("ListarLimitesEmpresa")]
    [EndpointSummary("Los cupos de la empresa, con su valor efectivo y de donde sale.")]
    [ProducesResponseType<IReadOnlyList<LimiteDeEmpresa>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarAsync(string slug, CancellationToken ct)
    {
        var resultado = await limites.ListarAsync(slug, ct);

        return resultado is null ? EmpresaNoEncontrada(slug) : Ok(resultado);
    }

    [HttpPut("{clave}")]
    [EndpointName("FijarLimiteEmpresa")]
    [EndpointSummary("Fija el cupo de un tipo para esta empresa. -1 es sin limite.")]
    [ProducesResponseType<IReadOnlyList<LimiteDeEmpresa>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FijarAsync(
        string slug, string clave, FijarLimite cuerpo, CancellationToken ct)
        => Responder(slug, await limites.FijarAsync(slug, clave, cuerpo.Valor, ct));

    [HttpDelete("{clave}")]
    [EndpointName("QuitarLimiteEmpresa")]
    [EndpointSummary("Devuelve el cupo al valor por defecto del catalogo.")]
    [ProducesResponseType<IReadOnlyList<LimiteDeEmpresa>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QuitarAsync(
        string slug, string clave, CancellationToken ct)
        => Responder(slug, await limites.QuitarAsync(slug, clave, ct));

    /// <summary>
    /// Los tres desenlaces, en un solo sitio: sin empresa es 404, rechazado es 400 con el
    /// motivo del servicio, y correcto devuelve la lista completa ya actualizada.
    ///
    /// El 200 con la lista y no un 204: la pantalla se repinta de esta respuesta, asi que
    /// devolver el estado nuevo le ahorra un GET y —lo que importa mas— le quita la
    /// oportunidad de quedarse mostrando el anterior.
    /// </summary>
    private IActionResult Responder(string slug, ResultadoLimites resultado)
    {
        if (!resultado.EmpresaExiste)
        {
            return EmpresaNoEncontrada(slug);
        }

        return resultado.Correcto
            ? Ok(resultado.Limites)
            : Problem(
                title: "Limite rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
    }

    private IActionResult EmpresaNoEncontrada(string slug)
        => Problem(
            title: "Empresa no encontrada",
            detail: $"No existe una empresa con el slug '{slug}'.",
            statusCode: StatusCodes.Status404NotFound);
}
