using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Catalogos;

/// <summary>
/// Categorias del catalogo de equipos.
///
/// USA LOS PERMISOS DE `equipos`, no unos propios, y esto vale para los siete catalogos de la
/// rebanada: no hay un modulo `catalogos` en ClavesModulo y no debe haberlo. Quien administra
/// categorias, tipos y marcas administra equipos; un modulo aparte le daria al plan comercial
/// una casilla que no significa nada para el cliente.
///
/// NO HAY DELETE. `categoria_equipo` no tiene `eliminado_en` y la FK de `tipo_equipo` impide
/// el borrado fisico en cuanto se use una vez, asi que retirar una categoria es
/// PATCH .../activo. El razonamiento esta en IServicioCategoriasEquipo.
/// </summary>
[ApiController]
[Route("api/catalogos/categorias-equipo")]
[Tags("Catalogos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class CategoriasEquipoController(IServicioCategoriasEquipo servicio)
    : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarCategoriasEquipo")]
    [EndpointSummary("Las categorias del catalogo, paginadas y filtrables.")]
    [ProducesResponseType<Pagina<CategoriaEquipoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync([FromQuery] Filtro filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ObtenerCategoriaEquipo")]
    [EndpointSummary("Una categoria por su id.")]
    [ProducesResponseType<CategoriaEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var categoria = await servicio.ObtenerAsync(id, ct);

        return categoria is null
            ? Problem(
                title: "No encontrado",
                detail: "La categoria no existe.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(categoria);
    }

    [HttpPost]
    [RequierePermiso("equipos.crear")]
    [EndpointName("CrearCategoriaEquipo")]
    [EndpointSummary("Crea una categoria de equipo.")]
    [ProducesResponseType<CategoriaEquipoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaCategoriaEquipo alta, CancellationToken ct)
        => this.AHttp(
            await servicio.CrearAsync(alta, ct),
            c => $"/api/catalogos/categorias-equipo/{c.Id}");

    [HttpPut("{id:guid}")]
    [RequierePermiso("equipos.editar")]
    [EndpointName("EditarCategoriaEquipo")]
    [EndpointSummary("Corrige el codigo, el nombre o la descripcion de una categoria.")]
    [ProducesResponseType<CategoriaEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarAsync(
        Guid id, AltaCategoriaEquipo cambio, CancellationToken ct)
        => this.AHttp(await servicio.EditarAsync(id, cambio, ct));

    /// <summary>
    /// Retira o reactiva la categoria. Ocupa el lugar del DELETE que no existe, y es un
    /// PATCH y no un DELETE porque no borra nada: la fila se queda y lo que cambia es si se
    /// ofrece.
    /// </summary>
    [HttpPatch("{id:guid}/activo")]
    [RequierePermiso("equipos.eliminar")]
    [EndpointName("CambiarActivoCategoriaEquipo")]
    [EndpointSummary("Retira o reactiva una categoria. No borra: los tipos siguen existiendo.")]
    [ProducesResponseType<CategoriaEquipoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        Guid id, CambioDeActivoCatalogo cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarActivoAsync(id, cambio.Activo, ct));
}

/// <summary>
/// El cuerpo del PATCH de activacion, compartido por los catalogos. Un objeto y no un
/// booleano suelto para que la peticion sea autoexplicativa en un log y para poder agregarle
/// campos —un motivo de retiro, el dia que se pida— sin cambiar la firma.
/// </summary>
public readonly record struct CambioDeActivoCatalogo(bool Activo);
