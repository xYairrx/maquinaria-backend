using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Procesos.Rentas;
using Maquinaria.Aplicacion.Rentas;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Maquinaria.Dominio.Comercial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Rentas;

/// <summary>
/// Rentas. **El criterio de salida de la fase.**
///
/// Los cuatro pasos que mueven el calendario son Procesos y tienen endpoint propio —confirmar,
/// extender, cerrar, cancelar—, no un PATCH de estado generico: cada uno hace mas que cambiar
/// una columna, y un PATCH que a veces ocupa el calendario y a veces no seria imposible de
/// documentar.
///
/// El PATCH de estado que si existe cubre solo los pasos que NO tocan nada: Confirmada → Activa
/// —el equipo salio— y Activa → Devuelta —regreso—.
/// </summary>
[ApiController]
[Route("api/rentas")]
[Tags("Rentas")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class RentasController(
    IServicioRentas servicio,
    ProcesoConfirmarRenta confirmar,
    ProcesoExtenderRenta extender,
    ProcesoCerrarRenta cierre,
    ProcesoRentaDesdeCotizacion desdeCotizacion) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("rentas.consultar")]
    [EndpointName("ListarRentas")]
    [EndpointSummary("Rentas, filtrables por cliente, equipo, estado y periodo. Sin lineas.")]
    [ProducesResponseType<Pagina<RentaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroRentas filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("rentas.consultar")]
    [EndpointName("ObtenerRenta")]
    [EndpointSummary("La renta con sus lineas y sus conceptos.")]
    [ProducesResponseType<RentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var renta = await servicio.ObtenerAsync(id, ct);

        return renta is null
            ? Problem(
                title: "No encontrado",
                detail: "La renta no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "renta",
                })
            : Ok(renta);
    }

    [HttpGet("{id:guid}/extensiones")]
    [RequierePermiso("rentas.consultar")]
    [EndpointName("ListarExtensionesDeRenta")]
    [ProducesResponseType<IReadOnlyList<ExtensionRentaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExtensionesAsync(Guid id, CancellationToken ct)
        => Ok(await servicio.ExtensionesAsync(id, ct));

    [HttpPost]
    [RequierePermiso("rentas.crear")]
    [EndpointName("CrearRenta")]
    [EndpointSummary("Crea una renta en Borrador. No ocupa calendario todavia.")]
    [ProducesResponseType<RentaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAsync(AltaRenta alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), r => $"/api/rentas/{r.Id}");

    /// <summary>
    /// Convierte una cotizacion Aceptada en una renta en Borrador, con los precios cotizados.
    /// Las lineas cotizadas por tipo de equipo se devuelven como pendientes de asignar.
    /// </summary>
    [HttpPost("desde-cotizacion/{cotizacionId:guid}")]
    [RequierePermiso("rentas.crear")]
    [EndpointName("CrearRentaDesdeCotizacion")]
    [EndpointSummary("Copia una cotizacion aceptada a una renta nueva, con precios congelados.")]
    [ProducesResponseType<ConversionDeCotizacion>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DesdeCotizacionAsync(
        Guid cotizacionId, ConversionARenta datos, CancellationToken ct)
        => this.AHttp(
            await desdeCotizacion.EjecutarAsync(cotizacionId, datos, ct),
            c => $"/api/rentas/{c.Renta.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("rentas.editar")]
    [EndpointName("EditarRenta")]
    [EndpointSummary("Solo en Borrador. Para alargar una renta en marcha, usa la extension.")]
    [ProducesResponseType<RentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(Guid id, AltaRenta cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    // ------------------------------------------------------------------- lineas ----

    [HttpPost("{id:guid}/lineas")]
    [RequierePermiso("rentas.editar")]
    [EndpointName("AgregarLineaARenta")]
    [EndpointSummary("Agrega un equipo. Solo en Borrador: despues tiene calendario detras.")]
    [ProducesResponseType<RentaLineaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AgregarLineaAsync(
        Guid id, AltaRentaLinea linea, CancellationToken ct)
        => this.AHttp(
            await servicio.AgregarLineaAsync(id, linea, ct), _ => $"/api/rentas/{id}");

    [HttpDelete("{id:guid}/lineas/{lineaId:guid}")]
    [RequierePermiso("rentas.editar")]
    [EndpointName("QuitarLineaDeRenta")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> QuitarLineaAsync(
        Guid id, Guid lineaId, CancellationToken ct)
        => this.AHttp(await servicio.QuitarLineaAsync(id, lineaId, ct));

    // ---------------------------------------------------------------- conceptos ----

    /// <summary>
    /// Flete, operador, maniobras. **Se pueden agregar con la renta ya en marcha**: un flete
    /// extra aparece cuando aparece, y no toca el calendario de ningun equipo.
    /// </summary>
    [HttpPost("{id:guid}/conceptos")]
    [RequierePermiso("rentas.editar")]
    [EndpointName("AgregarConceptoARenta")]
    [EndpointSummary("Agrega un cargo. El operador va aqui, con su trabajador.")]
    [ProducesResponseType<RentaConceptoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AgregarConceptoAsync(
        Guid id, AltaRentaConcepto concepto, CancellationToken ct)
        => this.AHttp(
            await servicio.AgregarConceptoAsync(id, concepto, ct), _ => $"/api/rentas/{id}");

    [HttpDelete("{id:guid}/conceptos/{conceptoId:guid}")]
    [RequierePermiso("rentas.editar")]
    [EndpointName("QuitarConceptoDeRenta")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> QuitarConceptoAsync(
        Guid id, Guid conceptoId, CancellationToken ct)
        => this.AHttp(await servicio.QuitarConceptoAsync(id, conceptoId, ct));

    // ----------------------------------------------------------------- procesos ----

    /// <summary>
    /// **Confirma la renta y ocupa el calendario de sus equipos.** Si alguno no esta libre,
    /// devuelve 409 diciendo cual y contra que choca, y **no confirma nada**.
    /// </summary>
    [HttpPost("{id:guid}/confirmacion")]
    [RequierePermiso("rentas.autorizar")]
    [EndpointName("ConfirmarRenta")]
    [EndpointSummary("Confirma y aparta los equipos. Todo o nada.")]
    [ProducesResponseType<RentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmarAsync(Guid id, CancellationToken ct)
        => this.AHttp(await confirmar.EjecutarAsync(id, ct));

    [HttpPost("{id:guid}/extensiones")]
    [RequierePermiso("rentas.autorizar")]
    [EndpointName("ExtenderRenta")]
    [EndpointSummary("Alarga la renta. El calendario se revalida y puede rechazar con 409.")]
    [ProducesResponseType<ExtensionRentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExtenderAsync(
        Guid id, AltaExtension alta, CancellationToken ct)
        => this.AHttp(await extender.EjecutarAsync(id, alta, ct));

    [HttpPost("{id:guid}/cierre")]
    [RequierePermiso("rentas.autorizar")]
    [EndpointName("CerrarRenta")]
    [EndpointSummary("Cierra la renta, registra horometros y libera el calendario.")]
    [ProducesResponseType<RentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CerrarAsync(
        Guid id, CierreDeRenta cierreDeRenta, CancellationToken ct)
        => this.AHttp(await cierre.CerrarAsync(id, cierreDeRenta, ct));

    [HttpPost("{id:guid}/cancelacion")]
    [RequierePermiso("rentas.autorizar")]
    [EndpointName("CancelarRenta")]
    [EndpointSummary("Cancela una renta en Borrador o Confirmada y libera el calendario.")]
    [ProducesResponseType<RentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelarAsync(Guid id, CancellationToken ct)
        => this.AHttp(await cierre.CancelarAsync(id, ct));

    /// <summary>
    /// Los dos pasos que NO tocan el calendario: el equipo salio —Activa— y el equipo regreso
    /// —Devuelta—. Todo lo demas tiene su propio endpoint porque hace mas que cambiar el estado.
    /// </summary>
    [HttpPatch("{id:guid}/estado")]
    [RequierePermiso("rentas.editar")]
    [EndpointName("CambiarEstadoRenta")]
    [EndpointSummary("Confirmada → Activa, Activa → Devuelta. No mueve el calendario.")]
    [ProducesResponseType<RentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstadoAsync(
        Guid id, CambioEstadoRenta cambio, CancellationToken ct)
    {
        // Los cuatro estados con Proceso propio se rechazan aqui: aceptarlos por este camino
        // saltaria la ocupacion o la liberacion del calendario.
        if (cambio.Estado is EstadoRenta.Confirmada or EstadoRenta.Cerrada
            or EstadoRenta.Cancelada)
        {
            return Problem(
                title: "Peticion rechazada",
                detail: $"{cambio.Estado} tiene su propio endpoint porque mueve el calendario.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return this.AHttp(await servicio.CambiarEstadoAsync(id, cambio.Estado, ct));
    }
}

public readonly record struct CambioEstadoRenta(EstadoRenta Estado);
