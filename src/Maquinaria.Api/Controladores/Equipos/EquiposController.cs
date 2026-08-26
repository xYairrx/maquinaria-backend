using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Equipos;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Equipos;

/// <summary>
/// El parque de equipos.
///
/// AQUI SI HAY DELETE, y es logico: <c>equipo</c> es una de las tres tablas con
/// <c>eliminado_en</c>. Se rechaza si el calendario del equipo esta ocupado — un equipo
/// eliminado con una renta activa desaparece de las listas mientras sigue en la obra.
/// </summary>
[ApiController]
[Route("api/equipos")]
[Tags("Equipos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class EquiposController(IServicioEquipos servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarEquipos")]
    [EndpointSummary("El parque, filtrable por ubicacion, tipo, modelo, estado y proposito.")]
    [ProducesResponseType<Pagina<EquipoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroEquipos filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ObtenerEquipo")]
    [EndpointSummary("El expediente de un equipo.")]
    [ProducesResponseType<EquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var equipo = await servicio.ObtenerAsync(id, ct);

        return equipo is null
            ? Problem(
                title: "No encontrado",
                detail: "El equipo no existe.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(equipo);
    }

    [HttpPost]
    [RequierePermiso("equipos.crear")]
    [EndpointName("CrearEquipo")]
    [ProducesResponseType<EquipoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaEquipo alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), e => $"/api/equipos/{e.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("equipos.editar")]
    [EndpointName("EditarEquipo")]
    [EndpointSummary("Corrige el expediente. Mover la ubicacion aqui NO es un traspaso.")]
    [ProducesResponseType<EquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(Guid id, AltaEquipo cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    /// <summary>
    /// Disponible, EnMantenimiento, FueraDeServicio o Baja. Rentado, Reservado, EnTraslado y
    /// Vendido los pone la operacion y aqui se rechazan.
    /// </summary>
    [HttpPatch("{id:guid}/estado")]
    [RequierePermiso("equipos.editar")]
    [EndpointName("CambiarEstadoEquipo")]
    [EndpointSummary("Cambia el estado operativo. Exige que el calendario este libre.")]
    [ProducesResponseType<EquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstadoAsync(
        Guid id, CambioEstadoEquipo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarEstadoAsync(id, cambio, ct));

    [HttpDelete("{id:guid}")]
    [RequierePermiso("equipos.eliminar")]
    [EndpointName("EliminarEquipo")]
    [EndpointSummary("Borrado logico. Se rechaza si el equipo tiene calendario ocupado.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EliminarAsync(Guid id, CancellationToken ct)
        => this.AHttp(await servicio.EliminarAsync(id, ct));
}
