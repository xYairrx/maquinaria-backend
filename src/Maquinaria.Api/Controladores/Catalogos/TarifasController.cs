using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Catalogos;

/// <summary>
/// El catalogo de conceptos cobrables.
///
/// PERMISOS DE `rentas`, no de `equipos`: una tarifa es un concepto comercial, y quien la da
/// de alta es quien cotiza, no quien administra el parque.
/// </summary>
[ApiController]
[Route("api/catalogos/tarifas")]
[Tags("Catalogos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class TarifasController(IServicioTarifas servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("rentas.consultar")]
    [EndpointName("ListarTarifas")]
    [EndpointSummary("Las tarifas, filtrables por donde aplican y por unidad.")]
    [ProducesResponseType<Pagina<TarifaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroTarifas filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("rentas.consultar")]
    [EndpointName("ObtenerTarifa")]
    [ProducesResponseType<TarifaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var tarifa = await servicio.ObtenerAsync(id, ct);

        return tarifa is null
            ? Problem(
                title: "No encontrado",
                detail: "La tarifa no existe.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(tarifa);
    }

    [HttpPost]
    [RequierePermiso("rentas.crear")]
    [EndpointName("CrearTarifa")]
    [ProducesResponseType<TarifaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaTarifa alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), t => $"/api/catalogos/tarifas/{t.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("rentas.editar")]
    [EndpointName("EditarTarifa")]
    [ProducesResponseType<TarifaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(Guid id, AltaTarifa cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("rentas.eliminar")]
    [EndpointName("CambiarActivoTarifa")]
    [EndpointSummary("Retira o reactiva una tarifa. Los documentos ya emitidos no cambian.")]
    [ProducesResponseType<TarifaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}
