using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Catalogos;

/// <summary>
/// El catalogo de clausulas contractuales. Permisos de `contratos`.
///
/// Editar aqui NO toca ningun contrato ya generado: <c>contrato_clausula</c> guarda su propia
/// copia del titulo y del texto.
/// </summary>
[ApiController]
[Route("api/catalogos/clausulas")]
[Tags("Catalogos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class ClausulasController(IServicioClausulas servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("contratos.consultar")]
    [EndpointName("ListarClausulas")]
    [EndpointSummary("Las clausulas del catalogo, en su orden de impresion.")]
    [ProducesResponseType<Pagina<ClausulaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroClausulas filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("contratos.consultar")]
    [EndpointName("ObtenerClausula")]
    [ProducesResponseType<ClausulaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var clausula = await servicio.ObtenerAsync(id, ct);

        return clausula is null
            ? Problem(
                title: "No encontrado",
                detail: "La clausula no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "clausula",
                })
            : Ok(clausula);
    }

    [HttpPost]
    [RequierePermiso("contratos.crear")]
    [EndpointName("CrearClausula")]
    [ProducesResponseType<ClausulaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaClausula alta, CancellationToken ct)
        => this.AHttp(
            await servicio.CrearAsync(alta, ct), c => $"/api/catalogos/clausulas/{c.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("contratos.editar")]
    [EndpointName("EditarClausula")]
    [EndpointSummary("Corrige la plantilla. Los contratos ya generados no cambian.")]
    [ProducesResponseType<ClausulaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(
        Guid id, AltaClausula cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("contratos.eliminar")]
    [EndpointName("CambiarActivoClausula")]
    [ProducesResponseType<ClausulaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}
