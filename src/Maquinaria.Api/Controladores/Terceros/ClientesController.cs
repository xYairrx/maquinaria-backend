using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Terceros;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Maquinaria.Dominio.Terceros;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Terceros;

/// <summary>
/// Clientes, con su contacto y su domicilio dentro.
///
/// NO HAY DELETE: <c>cliente</c> no tiene <c>eliminado_en</c> y lo referencian cotizaciones,
/// rentas y contratos. Se retira con PATCH .../estado.
/// </summary>
[ApiController]
[Route("api/clientes")]
[Tags("Terceros")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class ClientesController(IServicioClientes servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("clientes.consultar")]
    [EndpointName("ListarClientes")]
    [EndpointSummary("Clientes, buscables por razon social, codigo o RFC.")]
    [ProducesResponseType<Pagina<ClienteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroClientes filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("clientes.consultar")]
    [EndpointName("ObtenerCliente")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var cliente = await servicio.ObtenerAsync(id, ct);

        return cliente is null
            ? Problem(
                title: "No encontrado",
                detail: "El cliente no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "cliente",
                })
            : Ok(cliente);
    }

    [HttpPost]
    [RequierePermiso("clientes.crear")]
    [EndpointName("CrearCliente")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaCliente alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), c => $"/api/clientes/{c.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("clientes.editar")]
    [EndpointName("EditarCliente")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(Guid id, AltaCliente cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/estado")]
    [RequierePermiso("clientes.eliminar")]
    [EndpointName("CambiarEstadoCliente")]
    [EndpointSummary("Activo, Suspendido o Baja. No toca las rentas abiertas.")]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstadoAsync(
        Guid id, CambioEstadoCliente cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarEstadoAsync(id, cambio.Estado, ct));
}

public readonly record struct CambioEstadoCliente(EstadoCliente Estado);
