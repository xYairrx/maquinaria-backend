using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Plataforma;

/// <summary>
/// El catalogo de modulos, que es lo que se usa para armar un plan.
///
/// CONTROLADOR APARTE Y NO UNA ACCION DE PlanesController, porque su ruta es otra:
/// /api/plataforma/modulos, no /api/plataforma/planes/... Con controladores la ruta base
/// vive en la clase, asi que dos recursos distintos son dos clases —y esta, de una sola
/// accion, es lo que cuesta esa regla—. En la version de Minimal API los dos colgaban del
/// mismo grupo /api/plataforma y por eso convivian en un archivo.
///
/// Los modulos NO se crean ni se editan desde aqui: la lista es codigo. Un modulo existe
/// porque hay pantallas y permisos que lo respaldan, asi que agregar uno es una migracion
/// y un despliegue, nunca un POST.
/// </summary>
[ApiController]
[Route("api/plataforma/modulos")]
[Tags("Plataforma")]
[Authorize(PoliticasAutorizacion.Plataforma)]
public sealed class ModulosController(ICatalogoPlanes catalogo) : ControllerBase
{
    [HttpGet]
    [EndpointName("ListarModulos")]
    [EndpointSummary("El catalogo de modulos activos, para armar un plan.")]
    [ProducesResponseType<IReadOnlyList<ResumenModulo>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(CancellationToken ct)
        => Ok(await catalogo.ListarModulosAsync(ct));
}
