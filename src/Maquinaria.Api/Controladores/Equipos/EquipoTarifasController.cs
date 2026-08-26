using Maquinaria.Aplicacion.Equipos;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Equipos;

/// <summary>
/// Los precios de un equipo. Cuelgan de el en la ruta porque no tienen sentido sueltos: un
/// precio siempre es «de este equipo, para este concepto».
///
/// NO HAY PUT. Un precio aplicado es un hecho con fecha; corregirlo reescribiria lo que estuvo
/// vigente. Se cierra el vigente con PATCH .../cierre y se carga el nuevo con POST.
/// </summary>
[ApiController]
[Route("api/equipos/{equipoId:guid}/tarifas")]
[Tags("Equipos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class EquipoTarifasController(IServicioEquipoTarifas servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarPreciosDeEquipo")]
    [EndpointSummary("Los precios cargados. Sin paginar: son unos pocos por concepto.")]
    [ProducesResponseType<IReadOnlyList<EquipoTarifaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        Guid equipoId, [FromQuery] bool soloVigentes, CancellationToken ct)
        => Ok(await servicio.ListarAsync(equipoId, soloVigentes, ct));

    [HttpPost]
    [RequierePermiso("equipos.editar")]
    [EndpointName("CargarPrecioDeEquipo")]
    [EndpointSummary("Carga un precio. Choca con 409 si ya hay uno vigente para esa combinacion.")]
    [ProducesResponseType<EquipoTarifaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(
        Guid equipoId, AltaEquipoTarifa alta, CancellationToken ct)
        => this.AHttp(
            await servicio.CrearAsync(equipoId, alta, ct),
            t => $"/api/equipos/{equipoId}/tarifas/{t.Id}");

    [HttpPatch("{id:guid}/cierre")]
    [RequierePermiso("equipos.editar")]
    [EndpointName("CerrarPrecioDeEquipo")]
    [EndpointSummary("Le pone fecha de fin a un precio vigente. Es como se cambia un precio.")]
    [ProducesResponseType<EquipoTarifaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CerrarAsync(
        Guid equipoId, Guid id, CierreDePrecio cierre, CancellationToken ct)
        => this.AHttp(await servicio.CerrarAsync(equipoId, id, cierre.VigenciaHasta, ct));
}

public readonly record struct CierreDePrecio(DateTime VigenciaHasta);
