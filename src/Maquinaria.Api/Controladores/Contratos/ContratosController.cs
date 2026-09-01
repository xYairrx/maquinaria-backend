using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Contratos;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Maquinaria.Dominio.Comercial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Contratos;

/// <summary>
/// Contratos de renta, con sus clausulas congeladas.
///
/// **NO HAY PUT NI DELETE del contrato**, y no es una omision: fuera de Borrador el motor lo
/// bloquea con un trigger, y en Borrador lo que se corrige son las clausulas. Un contrato mal
/// hecho en Borrador se queda en Borrador hasta que se arregla; uno autorizado ya no se toca.
///
/// **TAMPOCO HAY CANCELACION**: <c>EstadoContrato</c> no tiene ese valor en el esquema migrado.
/// Es una limitacion conocida, anotada en el plan de la fase.
/// </summary>
[ApiController]
[Route("api/contratos")]
[Tags("Contratos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class ContratosController(IServicioContratos servicio) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("contratos.consultar")]
    [EndpointName("ListarContratos")]
    [ProducesResponseType<Pagina<ContratoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(
        [FromQuery] FiltroContratos filtro, CancellationToken ct)
        => Ok(await servicio.ListarAsync(filtro, ct));

    [HttpGet("{id:guid}")]
    [RequierePermiso("contratos.consultar")]
    [EndpointName("ObtenerContrato")]
    [EndpointSummary("El contrato con sus clausulas, en su orden de impresion.")]
    [ProducesResponseType<ContratoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var contrato = await servicio.ObtenerAsync(id, ct);

        return contrato is null
            ? Problem(
                title: "No encontrado",
                detail: "El contrato no existe.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "contrato",
                })
            : Ok(contrato);
    }

    [HttpGet("por-renta/{rentaId:guid}")]
    [RequierePermiso("contratos.consultar")]
    [EndpointName("ObtenerContratoPorRenta")]
    [EndpointSummary("El contrato de una renta. Hay uno como maximo.")]
    [ProducesResponseType<ContratoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PorRentaAsync(Guid rentaId, CancellationToken ct)
    {
        var contrato = await servicio.PorRentaAsync(rentaId, ct);

        return contrato is null
            ? Problem(
                title: "No encontrado",
                detail: "Esa renta no tiene contrato.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.NoEncontrado,
                    ["entidad"] = "contrato_de_renta",
                })
            : Ok(contrato);
    }

    /// <summary>
    /// Genera el contrato de una renta y **congela** las clausulas del catalogo. Sin lista
    /// explicita, copia todas las obligatorias activas.
    /// </summary>
    [HttpPost]
    [RequierePermiso("contratos.crear")]
    [EndpointName("GenerarContrato")]
    [EndpointSummary("Un contrato por renta. Copia el texto de las clausulas, no lo referencia.")]
    [ProducesResponseType<ContratoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearAsync(AltaContrato alta, CancellationToken ct)
        => this.AHttp(await servicio.CrearAsync(alta, ct), c => $"/api/contratos/{c.Id}");

    /// <summary>
    /// Una clausula propia, negociada con ese cliente: se redacta aqui y no existe en el
    /// catalogo. Solo en Borrador.
    /// </summary>
    [HttpPost("{id:guid}/clausulas")]
    [RequierePermiso("contratos.editar")]
    [EndpointName("AgregarClausulaAContrato")]
    [ProducesResponseType<ContratoClausulaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AgregarClausulaAsync(
        Guid id, AltaContratoClausula clausula, CancellationToken ct)
        => this.AHttp(
            await servicio.AgregarClausulaAsync(id, clausula, ct), _ => $"/api/contratos/{id}");

    [HttpDelete("{id:guid}/clausulas/{clausulaId:guid}")]
    [RequierePermiso("contratos.editar")]
    [EndpointName("QuitarClausulaDeContrato")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> QuitarClausulaAsync(
        Guid id, Guid clausulaId, CancellationToken ct)
        => this.AHttp(await servicio.QuitarClausulaAsync(id, clausulaId, ct));

    /// <summary>
    /// Borrador → Autorizado → Firmado → Terminado. **Autorizar es el punto sin retorno**: de
    /// ahi en adelante el trigger del motor bloquea toda edicion del contrato y de sus
    /// clausulas.
    /// </summary>
    [HttpPatch("{id:guid}/estado")]
    [RequierePermiso("contratos.autorizar")]
    [EndpointName("CambiarEstadoContrato")]
    [EndpointSummary("Autorizar exige clausulas y vuelve el contrato inmutable.")]
    [ProducesResponseType<ContratoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstadoAsync(
        Guid id, CambioEstadoContrato cambio, CancellationToken ct)
        => this.AHttp(await servicio.CambiarEstadoAsync(id, cambio.Estado, ct));
}

public readonly record struct CambioEstadoContrato(EstadoContrato Estado);
