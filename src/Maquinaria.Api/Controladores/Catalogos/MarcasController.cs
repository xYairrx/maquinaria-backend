using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Catalogos;

/// <summary>
/// Marcas de maquinaria. Mismos permisos que las categorias —los de `equipos`— y misma
/// ausencia de DELETE: se retira con PATCH .../activo.
/// </summary>
[ApiController]
[Route("api/catalogos/marcas")]
[Tags("Catalogos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class MarcasController(IServicioMarcas servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarMarcas")]
    [EndpointSummary("Las marcas del catalogo, paginadas y filtrables.")]
    [ProducesResponseType<Pagina<MarcaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync([FromQuery] Filtro filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ObtenerMarca")]
    [EndpointSummary("Una marca por su id.")]
    [ProducesResponseType<MarcaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var marca = await servicio.ObtenerAsync(id, ct);

        return marca is null
            ? Problem(
                title: "No encontrado",
                detail: "La marca no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "marca",
                })
            : Ok(marca);
    }

    [HttpPost]
    [RequierePermiso("equipos.crear")]
    [EndpointName("CrearMarca")]
    [EndpointSummary("Crea una marca.")]
    [ProducesResponseType<MarcaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaMarca alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), m => $"/api/catalogos/marcas/{m.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("equipos.editar")]
    [EndpointName("EditarMarca")]
    [EndpointSummary("Corrige el nombre de una marca.")]
    [ProducesResponseType<MarcaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(Guid id, AltaMarca cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("equipos.eliminar")]
    [EndpointName("CambiarActivoMarca")]
    [EndpointSummary("Retira o reactiva una marca. No borra: los modelos siguen existiendo.")]
    [ProducesResponseType<MarcaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}
