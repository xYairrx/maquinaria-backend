using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Api.Seguridad;
using Maquinaria.Dominio.Plataforma;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Plataforma;

/// <summary>
/// El catalogo de TIPOS de limite: que limites sabe nombrar el sistema.
///
/// Recurso propio y no acciones de <see cref="LimitesController"/>, porque no cuelga de
/// ninguna empresa: `/api/plataforma/limites` es el catalogo, y
/// `/api/plataforma/empresas/{slug}/limites` son los cupos de una. Es el mismo reparto que
/// hay entre `/planes` y la suscripcion de un tenant.
///
/// NO HAY DELETE, y es la regla del modelo, no una omision: la FK de `tenant_limite` es
/// RESTRICT justamente para impedir que se borre un tipo que alguien tiene negociado. Un
/// tipo se retira con `activo = false`.
/// </summary>
[ApiController]
[Route("api/plataforma/limites")]
[Tags("Plataforma")]
[Authorize(PoliticasAutorizacion.Plataforma)]
public sealed class TiposLimiteController(ICatalogoLimites catalogo) : ControllerBase
{
    [HttpGet]
    [EndpointName("ListarTiposLimite")]
    [EndpointSummary("El catalogo de tipos de limite, activos e inactivos.")]
    [ProducesResponseType<IReadOnlyList<ResumenTipoLimite>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(CancellationToken ct)
        => Ok(await catalogo.ListarAsync(ct));

    [HttpPost]
    [EndpointName("CrearTipoLimite")]
    [EndpointSummary(
        "Crea un tipo de limite. OJO: crear el tipo no crea el limite — solo lo nombra.")]
    [ProducesResponseType<ResumenTipoLimite>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAsync(AltaTipoLimite alta, CancellationToken ct)
    {
        var resultado = await catalogo.CrearAsync(alta, ct);

        if (!resultado.Correcto)
        {
            // Todo lo que rechaza el catalogo es dato mal capturado, asi que 400 siempre:
            // no hay aqui el tercer desenlace del alta de empresas, donde algo puede
            // romperse a medio camino y quedar reintentable.
            return Problem(
                title: "Tipo de limite rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var tipo = resultado.Tipo!;

        return Created($"/api/plataforma/limites/{tipo.Clave}", tipo);
    }

    [HttpPatch("{clave}")]
    [EndpointName("EditarTipoLimite")]
    [EndpointSummary(
        "Edita un tipo. Mover el valor por defecto cambia el cupo de toda empresa sin excepcion.")]
    [ProducesResponseType<ResumenTipoLimite>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EditarAsync(
        string clave, CambioTipoLimite cambio, CancellationToken ct)
    {
        var resultado = await catalogo.EditarAsync(
            FormatoClaveLimite.Normalizar(clave), cambio, ct);

        return resultado.Correcto
            ? Ok(resultado.Tipo)
            : Problem(
                title: "Tipo de limite rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
    }
}
