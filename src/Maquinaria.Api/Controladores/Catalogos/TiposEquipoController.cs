using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Catalogos;

/// <summary>Tipos de equipo. Cuelgan de una categoria y los equipos cuelgan de ellos.</summary>
[ApiController]
[Route("api/catalogos/tipos-equipo")]
[Tags("Catalogos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class TiposEquipoController(IServicioTiposEquipo servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarTiposEquipo")]
    [EndpointSummary("Los tipos de equipo, filtrables por categoria.")]
    [ProducesResponseType<Pagina<TipoEquipoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroTiposEquipo filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ObtenerTipoEquipo")]
    [ProducesResponseType<TipoEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var tipo = await servicio.ObtenerAsync(id, ct);

        return tipo is null
            ? Problem(
                title: "No encontrado",
                detail: "El tipo de equipo no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "tipo_equipo",
                })
            : Ok(tipo);
    }

    [HttpPost]
    [RequierePermiso("equipos.crear")]
    [EndpointName("CrearTipoEquipo")]
    [ProducesResponseType<TipoEquipoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaTipoEquipo alta, CancellationToken ct)
        => this.AHttp(
            await servicio.CrearAsync(alta, ct), t => $"/api/catalogos/tipos-equipo/{t.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("equipos.editar")]
    [EndpointName("EditarTipoEquipo")]
    [ProducesResponseType<TipoEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(
        Guid id, AltaTipoEquipo cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("equipos.eliminar")]
    [EndpointName("CambiarActivoTipoEquipo")]
    [EndpointSummary("Retira o reactiva un tipo. No borra: los equipos siguen existiendo.")]
    [ProducesResponseType<TipoEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}
