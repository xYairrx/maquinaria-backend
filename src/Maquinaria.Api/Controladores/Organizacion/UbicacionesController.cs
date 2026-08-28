using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Organizacion;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Controladores.Catalogos;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Organizacion;

/// <summary>
/// Bodegas, sucursales y patios. Permisos de `sucursales`.
///
/// El DTO expone `almacenaEquipo` y `esAdministrativa` de SOLO LECTURA: se derivan del tipo y
/// el alta no las acepta. Mandarlas no es un error de validacion, simplemente no existen en el
/// cuerpo de entrada.
/// </summary>
[ApiController]
[Route("api/ubicaciones")]
[Tags("Organizacion")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class UbicacionesController(IServicioUbicaciones servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("sucursales.consultar")]
    [EndpointName("ListarUbicaciones")]
    [EndpointSummary("Ubicaciones, filtrables por tipo y por capacidad.")]
    [ProducesResponseType<Pagina<UbicacionDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroUbicaciones filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("sucursales.consultar")]
    [EndpointName("ObtenerUbicacion")]
    [ProducesResponseType<UbicacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var ubicacion = await servicio.ObtenerAsync(id, ct);

        return ubicacion is null
            ? Problem(
                title: "No encontrado",
                detail: "La ubicacion no existe.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(ubicacion);
    }

    [HttpPost]
    [RequierePermiso("sucursales.crear")]
    [EndpointName("CrearUbicacion")]
    [ProducesResponseType<UbicacionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaUbicacion alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), u => $"/api/ubicaciones/{u.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("sucursales.editar")]
    [EndpointName("EditarUbicacion")]
    [EndpointSummary("Corrige la ubicacion. Bajar el tipo a uno que no almacena exige que este vacia.")]
    [ProducesResponseType<UbicacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(
        Guid id, AltaUbicacion cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("sucursales.eliminar")]
    [EndpointName("CambiarActivoUbicacion")]
    [ProducesResponseType<UbicacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}
