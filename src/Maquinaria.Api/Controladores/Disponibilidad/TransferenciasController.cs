using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Aplicacion.Procesos.Disponibilidad;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Disponibilidad;

/// <summary>
/// Traspasos de equipo entre bodegas y patios.
///
/// NO HAY PUT NI DELETE: un traspaso es un hecho con fecha. Si se capturo mal, se traspasa de
/// vuelta — que es lo que de verdad paso.
/// </summary>
[ApiController]
[Route("api/transferencias")]
[Tags("Disponibilidad")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class TransferenciasController(
    IServicioTransferencias servicio,
    ProcesoTraspasarEquipo proceso) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarTransferencias")]
    [EndpointSummary("Historial de traspasos, filtrable por equipo o por ubicacion.")]
    [ProducesResponseType<Pagina<TransferenciaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroTransferencias filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    /// <summary>
    /// El origen NO viene en el cuerpo: es donde esta el equipo ahora. Aceptarlo permitiria
    /// registrar un traspaso «desde» una bodega en la que la maquina no estaba.
    /// </summary>
    [HttpPost]
    [RequierePermiso("equipos.editar")]
    [EndpointName("TraspasarEquipo")]
    [EndpointSummary("Traspasa un equipo. Solo entre ubicaciones que almacenan.")]
    [ProducesResponseType<TransferenciaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TraspasarAsync(AltaTransferencia alta, CancellationToken ct)
        => this.AHttp(
            await proceso.EjecutarAsync(alta, ct), t => $"/api/transferencias?equipoId={t.EquipoId}");
}
