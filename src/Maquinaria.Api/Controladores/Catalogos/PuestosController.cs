using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Catalogos;

/// <summary>
/// Puestos de la organizacion. Permisos de `usuarios`: es quien administra a la gente, aunque
/// un trabajador no sea una cuenta.
/// </summary>
[ApiController]
[Route("api/catalogos/puestos")]
[Tags("Catalogos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class PuestosController(IServicioPuestos servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("usuarios.consultar")]
    [EndpointName("ListarPuestos")]
    [ProducesResponseType<Pagina<PuestoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync([FromQuery] Filtro filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("usuarios.consultar")]
    [EndpointName("ObtenerPuesto")]
    [ProducesResponseType<PuestoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var puesto = await servicio.ObtenerAsync(id, ct);

        return puesto is null
            ? Problem(
                title: "No encontrado",
                detail: "El puesto no existe.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(puesto);
    }

    [HttpPost]
    [RequierePermiso("usuarios.crear")]
    [EndpointName("CrearPuesto")]
    [ProducesResponseType<PuestoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaPuesto alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), p => $"/api/catalogos/puestos/{p.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("usuarios.editar")]
    [EndpointName("EditarPuesto")]
    [ProducesResponseType<PuestoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(Guid id, AltaPuesto cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("usuarios.eliminar")]
    [EndpointName("CambiarActivoPuesto")]
    [ProducesResponseType<PuestoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}
