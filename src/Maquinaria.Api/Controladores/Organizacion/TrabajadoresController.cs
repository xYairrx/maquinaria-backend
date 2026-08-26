using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Organizacion;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Organizacion;

/// <summary>
/// Las personas de la organizacion. Permisos de `usuarios`.
///
/// NO HAY DELETE ni PATCH de activo: se retira con PATCH .../estado, que es donde la fecha de
/// baja viaja junto con el estado como el CHECK de la base exige.
/// </summary>
[ApiController]
[Route("api/trabajadores")]
[Tags("Organizacion")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class TrabajadoresController(IServicioTrabajadores servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("usuarios.consultar")]
    [EndpointName("ListarTrabajadores")]
    [EndpointSummary("Trabajadores, filtrables por puesto, ubicacion y estado.")]
    [ProducesResponseType<Pagina<TrabajadorDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroTrabajadores filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("usuarios.consultar")]
    [EndpointName("ObtenerTrabajador")]
    [ProducesResponseType<TrabajadorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var trabajador = await servicio.ObtenerAsync(id, ct);

        return trabajador is null
            ? Problem(
                title: "No encontrado",
                detail: "El trabajador no existe.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(trabajador);
    }

    [HttpPost]
    [RequierePermiso("usuarios.crear")]
    [EndpointName("CrearTrabajador")]
    [ProducesResponseType<TrabajadorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaTrabajador alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), t => $"/api/trabajadores/{t.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("usuarios.editar")]
    [EndpointName("EditarTrabajador")]
    [ProducesResponseType<TrabajadorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(
        Guid id, AltaTrabajador cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    /// <summary>
    /// El estado y la fecha de baja viajan juntos porque el CHECK de la base exige que el
    /// estado Baja y la fecha existan a la vez. Separarlos en dos llamadas dejaria un momento
    /// en que la fila es invalida.
    /// </summary>
    [HttpPatch("{id:guid}/estado")]
    [RequierePermiso("usuarios.editar")]
    [EndpointName("CambiarEstadoTrabajador")]
    [EndpointSummary("Activo, Inactivo o Baja. La baja exige su fecha.")]
    [ProducesResponseType<TrabajadorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstadoAsync(
        Guid id, CambioEstadoTrabajador cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarEstadoAsync(id, cambio, ct));
}
