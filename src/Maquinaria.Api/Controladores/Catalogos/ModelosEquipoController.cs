using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Catalogos;

/// <summary>Modelos de equipo. Su clave unica es la marca mas el nombre.</summary>
[ApiController]
[Route("api/catalogos/modelos-equipo")]
[Tags("Catalogos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class ModelosEquipoController(IServicioModelosEquipo servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarModelosEquipo")]
    [EndpointSummary("Los modelos, filtrables por marca y por tipo.")]
    [ProducesResponseType<Pagina<ModeloEquipoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroModelosEquipo filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ObtenerModeloEquipo")]
    [ProducesResponseType<ModeloEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var modelo = await servicio.ObtenerAsync(id, ct);

        return modelo is null
            ? Problem(
                title: "No encontrado",
                detail: "El modelo no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "modelo",
                })
            : Ok(modelo);
    }

    [HttpPost]
    [RequierePermiso("equipos.crear")]
    [EndpointName("CrearModeloEquipo")]
    [ProducesResponseType<ModeloEquipoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaModeloEquipo alta, CancellationToken ct)
        => this.AHttp(
            await servicio.CrearAsync(alta, ct), m => $"/api/catalogos/modelos-equipo/{m.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("equipos.editar")]
    [EndpointName("EditarModeloEquipo")]
    [ProducesResponseType<ModeloEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(
        Guid id, AltaModeloEquipo cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("equipos.eliminar")]
    [EndpointName("CambiarActivoModeloEquipo")]
    [ProducesResponseType<ModeloEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}
