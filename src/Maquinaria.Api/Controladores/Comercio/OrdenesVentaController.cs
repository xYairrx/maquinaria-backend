using Maquinaria.Aplicacion.Comercio;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Procesos.Comercio;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Comercio;

/// <summary>
/// Ordenes de venta de equipo. Mismo flujo que la compra, simetrico.
///
/// **Finalizar saca los equipos del parque y les cierra el calendario**, asi que tiene endpoint
/// propio. Si alguno tiene una renta abierta que se cruza, la finalizacion se rechaza con 409:
/// no se entrega una maquina que esta en la obra de otro cliente.
/// </summary>
[ApiController]
[Route("api/ordenes-venta")]
[Tags("Comercio")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class OrdenesVentaController(
    IServicioOrdenesVenta servicio,
    ProcesoFinalizarOrdenVenta finalizar) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("compras.consultar")]
    [EndpointName("ListarOrdenesVenta")]
    [ProducesResponseType<Pagina<OrdenVentaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroOrdenes filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("compras.consultar")]
    [EndpointName("ObtenerOrdenVenta")]
    [ProducesResponseType<OrdenVentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var orden = await servicio.ObtenerAsync(id, ct);

        return orden is null
            ? Problem(
                title: "No encontrado",
                detail: "La orden no existe.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(orden);
    }

    [HttpPost]
    [RequierePermiso("compras.crear")]
    [EndpointName("CrearOrdenVenta")]
    [ProducesResponseType<OrdenVentaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAsync(AltaOrdenVenta alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), o => $"/api/ordenes-venta/{o.Id}");

    [HttpPost("{id:guid}/detalles")]
    [RequierePermiso("compras.editar")]
    [EndpointName("AgregarDetalleAOrdenVenta")]
    [EndpointSummary("Agrega un equipo. Su proposito tiene que incluir Venta.")]
    [ProducesResponseType<OrdenVentaDetalleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AgregarDetalleAsync(
        Guid id, AltaOrdenVentaDetalle detalle, CancellationToken ct)
        => this.AHttp(
            await servicio.AgregarDetalleAsync(id, detalle, ct),
            _ => $"/api/ordenes-venta/{id}");

    [HttpDelete("{id:guid}/detalles/{detalleId:guid}")]
    [RequierePermiso("compras.editar")]
    [EndpointName("QuitarDetalleDeOrdenVenta")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> QuitarDetalleAsync(
        Guid id, Guid detalleId, CancellationToken ct)
        => this.AHttp(await servicio.QuitarDetalleAsync(id, detalleId, ct));

    [HttpPatch("{id:guid}/estado")]
    [RequierePermiso("compras.autorizar")]
    [EndpointName("CambiarEstadoOrdenVenta")]
    [EndpointSummary("Autorizar o cancelar. Finalizar tiene su propio endpoint.")]
    [ProducesResponseType<OrdenVentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstadoAsync(
        Guid id, CambioEstadoOrden cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarEstadoAsync(id, cambio.Estado, ct));

    /// <summary>
    /// Finaliza la venta: marca los equipos Vendidos y **les cierra el calendario** con una
    /// ocupacion sin fecha de fin. A partir de ahi no vuelven a aparecer como disponibles.
    /// </summary>
    [HttpPost("{id:guid}/finalizacion")]
    [RequierePermiso("compras.autorizar")]
    [EndpointName("FinalizarOrdenVenta")]
    [EndpointSummary("Saca los equipos del parque y cierra su calendario. Todo o nada.")]
    [ProducesResponseType<OrdenVentaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FinalizarAsync(Guid id, CancellationToken ct)
        => this.AHttp(await finalizar.EjecutarAsync(id, ct));
}
