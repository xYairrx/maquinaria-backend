using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Cotizaciones;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Maquinaria.Dominio.Comercial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Cotizaciones;

/// <summary>
/// Cotizaciones. El primer paso del ciclo <c>cotizar → aprobar → rentar → cerrar</c>.
///
/// Las lineas cuelgan de la cotizacion en la ruta y solo se tocan en Borrador. El folio lo pone
/// el sistema: no viene en el cuerpo.
/// </summary>
[ApiController]
[Route("api/cotizaciones")]
[Tags("Cotizaciones")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class CotizacionesController(IServicioCotizaciones servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("cotizaciones.consultar")]
    [EndpointName("ListarCotizaciones")]
    [EndpointSummary("Cotizaciones, filtrables por cliente, estado y fechas. Sin lineas.")]
    [ProducesResponseType<Pagina<CotizacionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroCotizaciones filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("cotizaciones.consultar")]
    [EndpointName("ObtenerCotizacion")]
    [EndpointSummary("La cotizacion con sus lineas.")]
    [ProducesResponseType<CotizacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var cotizacion = await servicio.ObtenerAsync(id, ct);

        return cotizacion is null
            ? Problem(
                title: "No encontrado",
                detail: "La cotizacion no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "cotizacion",
                })
            : Ok(cotizacion);
    }

    [HttpPost]
    [RequierePermiso("cotizaciones.crear")]
    [EndpointName("CrearCotizacion")]
    [EndpointSummary("Crea una cotizacion en Borrador. Solo desde una sucursal o un patio.")]
    [ProducesResponseType<CotizacionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAsync(AltaCotizacion alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), c => $"/api/cotizaciones/{c.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("cotizaciones.editar")]
    [EndpointName("EditarCotizacion")]
    [EndpointSummary("Solo en Borrador.")]
    [ProducesResponseType<CotizacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(
        Guid id, AltaCotizacion cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    /// <summary>
    /// Borrador → Enviada → EnRevision → Aceptada / Rechazada / Vencida, y Cancelada desde
    /// cualquiera menos las terminales. Enviar exige que haya lineas.
    /// </summary>
    [HttpPatch("{id:guid}/estado")]
    [RequierePermiso("cotizaciones.autorizar")]
    [EndpointName("CambiarEstadoCotizacion")]
    [EndpointSummary("Mueve el estado. Las transiciones invalidas devuelven 409.")]
    [ProducesResponseType<CotizacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstadoAsync(
        Guid id, CambioEstadoCotizacion cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarEstadoAsync(id, cambio.Estado, ct));

    [HttpPost("{id:guid}/lineas")]
    [RequierePermiso("cotizaciones.editar")]
    [EndpointName("AgregarLineaACotizacion")]
    [EndpointSummary("Agrega una linea. El importe se calcula: cantidad por precio.")]
    [ProducesResponseType<CotizacionLineaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AgregarLineaAsync(
        Guid id, AltaCotizacionLinea linea, CancellationToken ct)
        => this.AHttp(
            await servicio.AgregarLineaAsync(id, linea, ct),
            _ => $"/api/cotizaciones/{id}");

    [HttpDelete("{id:guid}/lineas/{lineaId:guid}")]
    [RequierePermiso("cotizaciones.editar")]
    [EndpointName("QuitarLineaDeCotizacion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> QuitarLineaAsync(
        Guid id, Guid lineaId, CancellationToken ct)
        => this.AHttp(await servicio.QuitarLineaAsync(id, lineaId, ct));
}

public readonly record struct CambioEstadoCotizacion(EstadoCotizacion Estado);
