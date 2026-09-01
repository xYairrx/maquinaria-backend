using Maquinaria.Aplicacion.Comercio;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Procesos.Comercio;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Maquinaria.Dominio.Comercial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Comercio;

/// <summary>
/// Ordenes de compra de equipo. Borrador → Autorizada → Finalizada, mas Cancelada.
///
/// **Finalizar registra los equipos en el catalogo**, asi que tiene endpoint propio y pide el
/// codigo interno de cada maquina. Una linea, una maquina.
/// </summary>
[ApiController]
[Route("api/ordenes-compra")]
[Tags("Comercio")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class OrdenesCompraController(
    IServicioOrdenesCompra servicio,
    ProcesoFinalizarOrdenCompra finalizar) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("compras.consultar")]
    [EndpointName("ListarOrdenesCompra")]
    [ProducesResponseType<Pagina<OrdenCompraDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroOrdenes filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("compras.consultar")]
    [EndpointName("ObtenerOrdenCompra")]
    [ProducesResponseType<OrdenCompraDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var orden = await servicio.ObtenerAsync(id, ct);

        return orden is null
            ? Problem(
                title: "No encontrado",
                detail: "La orden no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "orden_compra",
                })
            : Ok(orden);
    }

    [HttpPost]
    [RequierePermiso("compras.crear")]
    [EndpointName("CrearOrdenCompra")]
    [ProducesResponseType<OrdenCompraDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAsync(AltaOrdenCompra alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), o => $"/api/ordenes-compra/{o.Id}");

    [HttpPost("{id:guid}/detalles")]
    [RequierePermiso("compras.editar")]
    [EndpointName("AgregarDetalleAOrdenCompra")]
    [EndpointSummary("Una linea por maquina, con su numero de serie. Cantidad tiene que ser 1.")]
    [ProducesResponseType<OrdenCompraDetalleDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AgregarDetalleAsync(
        Guid id, AltaOrdenCompraDetalle detalle, CancellationToken ct)
        => this.AHttp(
            await servicio.AgregarDetalleAsync(id, detalle, ct),
            _ => $"/api/ordenes-compra/{id}");

    [HttpDelete("{id:guid}/detalles/{detalleId:guid}")]
    [RequierePermiso("compras.editar")]
    [EndpointName("QuitarDetalleDeOrdenCompra")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> QuitarDetalleAsync(
        Guid id, Guid detalleId, CancellationToken ct)
        => this.AHttp(await servicio.QuitarDetalleAsync(id, detalleId, ct));

    [HttpPatch("{id:guid}/estado")]
    [RequierePermiso("compras.autorizar")]
    [EndpointName("CambiarEstadoOrdenCompra")]
    [EndpointSummary("Autorizar o cancelar. Finalizar tiene su propio endpoint.")]
    [ProducesResponseType<OrdenCompraDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstadoAsync(
        Guid id, CambioEstadoOrden cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarEstadoAsync(id, cambio.Estado, ct));

    /// <summary>
    /// Finaliza la orden y **da de alta las maquinas**. Cada linea sin equipo necesita su
    /// registro con el codigo interno y el tipo; si falta uno, no se finaliza nada.
    /// </summary>
    [HttpPost("{id:guid}/finalizacion")]
    [RequierePermiso("compras.autorizar")]
    [EndpointName("FinalizarOrdenCompra")]
    [EndpointSummary("Registra los equipos en el catalogo y cierra la orden. Todo o nada.")]
    [ProducesResponseType<OrdenCompraDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> FinalizarAsync(
        Guid id, FinalizacionDeCompra finalizacion, CancellationToken ct)
        => this.AHttp(await finalizar.EjecutarAsync(id, finalizacion.Equipos, ct));
}

public readonly record struct CambioEstadoOrden(EstadoOrden Estado);

public sealed record FinalizacionDeCompra(IReadOnlyList<RegistroDeEquipo> Equipos);
