using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Terceros;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Controladores.Catalogos;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Terceros;

/// <summary>Proveedores. Permisos propios: `proveedores`.</summary>
[ApiController]
[Route("api/proveedores")]
[Tags("Terceros")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class ProveedoresController(IServicioProveedores servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("proveedores.consultar")]
    [EndpointName("ListarProveedores")]
    [ProducesResponseType<Pagina<ProveedorDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync([FromQuery] Filtro filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("proveedores.consultar")]
    [EndpointName("ObtenerProveedor")]
    [ProducesResponseType<ProveedorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var proveedor = await servicio.ObtenerAsync(id, ct);

        return proveedor is null
            ? Problem(
                title: "No encontrado",
                detail: "El proveedor no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "proveedor",
                })
            : Ok(proveedor);
    }

    [HttpPost]
    [RequierePermiso("proveedores.crear")]
    [EndpointName("CrearProveedor")]
    [ProducesResponseType<ProveedorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaProveedor alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), p => $"/api/proveedores/{p.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("proveedores.editar")]
    [EndpointName("EditarProveedor")]
    [ProducesResponseType<ProveedorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(
        Guid id, AltaProveedor cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("proveedores.eliminar")]
    [EndpointName("CambiarActivoProveedor")]
    [ProducesResponseType<ProveedorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}
