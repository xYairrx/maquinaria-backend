using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Disponibilidad;

/// <summary>
/// Disponibilidad: que equipos hay libres en un periodo, y el calendario de cada uno.
///
/// Es la pregunta que el M3 describe como consultar «rentas, reservas, mantenimiento, bloqueos
/// y traslados». **No se contesta consultando cinco tablas**: todo lo que ocupa un equipo vive
/// en <c>ocupacion_equipo</c>, asi que es UNA consulta con indice GiST.
/// </summary>
[ApiController]
[Route("api/disponibilidad")]
[Tags("Disponibilidad")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class DisponibilidadController(IServicioOcupacion servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("disponibilidad.consultar")]
    [EndpointName("ConsultarDisponibilidad")]
    [EndpointSummary("Equipos libres entre dos fechas, con su precio de renta diaria.")]
    [ProducesResponseType<Pagina<EquipoDisponibleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DisponiblesAsync(
        [FromQuery] FiltroDisponibilidad filtro, CancellationToken ct)
    {
        // Las fechas son obligatorias y se validan aqui porque sin ellas la consulta no
        // significa nada: «que hay disponible» sin periodo no es una pregunta que esta tabla
        // pueda contestar. Es forma, no negocio.
        if (filtro.Desde == default || filtro.Hasta == default)
        {
            return Problem(
                title: "Peticion rechazada",
                detail: "El periodo es obligatorio: manda desde y hasta.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (filtro.Hasta <= filtro.Desde)
        {
            return Problem(
                title: "Peticion rechazada",
                detail: "La fecha final tiene que ser posterior a la inicial.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Ok(await servicio.DisponiblesAsync(filtro, ct));
    }

    [HttpGet("equipos/{equipoId:guid}")]
    [RequierePermiso("disponibilidad.consultar")]
    [EndpointName("ConsultarCalendarioDeEquipo")]
    [EndpointSummary("El calendario de un equipo, incluidas las ocupaciones ya liberadas.")]
    [ProducesResponseType<IReadOnlyList<OcupacionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CalendarioAsync(
        Guid equipoId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        CancellationToken ct)
        => Ok(await servicio.CalendarioAsync(
            equipoId,
            // Sin `desde` se asume hoy: el calendario que interesa por defecto es el que
            // viene, no el historico completo.
            desde ?? DateTime.UtcNow.Date,
            hasta,
            ct));

    /// <summary>
    /// Bloquea el calendario a mano: mantenimiento, reparacion o bloqueo administrativo.
    ///
    /// Renta, Reserva y Traslado NO se pueden capturar aqui: salen de un documento, y el
    /// servicio los rechaza.
    /// </summary>
    [HttpPost("bloqueos")]
    [RequierePermiso("disponibilidad.crear")]
    [EndpointName("BloquearCalendarioDeEquipo")]
    [EndpointSummary("Ocupa el calendario por mantenimiento, reparacion o bloqueo.")]
    [ProducesResponseType<OcupacionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BloquearAsync(AltaBloqueo alta, CancellationToken ct)
        => this.AHttp(
            await servicio.BloquearAsync(alta, ct),
            o => $"/api/disponibilidad/equipos/{o.EquipoId}");

    [HttpDelete("bloqueos/{id:guid}")]
    [RequierePermiso("disponibilidad.eliminar")]
    [EndpointName("LiberarBloqueo")]
    [EndpointSummary("Libera un bloqueo manual. No borra la fila: la marca inactiva.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LiberarAsync(Guid id, CancellationToken ct)
        => this.AHttp(await servicio.LiberarAsync(id, ct));
}
