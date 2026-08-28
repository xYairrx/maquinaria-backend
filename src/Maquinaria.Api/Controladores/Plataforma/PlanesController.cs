using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Api.Seguridad;
using Maquinaria.Dominio.Plataforma;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Plataforma;

/// <summary>
/// El catalogo comercial: los planes. Solo la plataforma.
///
/// NO HAY PUT NI PATCH DE UN PLAN ENTERO, y es una decision, no una omision. `Suscripcion`
/// no guarda importe —solo apunta al plan— y el plan ES su conjunto de modulos, asi que
/// editar el precio reescribiria lo que pagaron los suscriptores historicos y editar los
/// modulos cambiaria el acceso de los actuales, retroactivamente. Lo que si se puede es
/// retirar un plan y crear su sucesor, que es lo que el modelo contempla con `activo`.
/// El razonamiento largo esta en `CrearPlan`.
/// </summary>
[ApiController]
[Route("api/plataforma/planes")]
[Tags("Plataforma")]
[Authorize(PoliticasAutorizacion.Plataforma)]
public sealed class PlanesController(ICatalogoPlanes catalogo, CrearPlan crearPlan)
    : ControllerBase
{
    [HttpGet]
    [EndpointName("ListarPlanes")]
    [EndpointSummary("Los planes del catalogo, activos e inactivos, con sus modulos.")]
    [ProducesResponseType<IReadOnlyList<ResumenPlan>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(CancellationToken ct)
        => Ok(await catalogo.ListarPlanesAsync(ct));

    [HttpPost]
    [EndpointName("CrearPlan")]
    [EndpointSummary("Crea un plan con su conjunto de modulos.")]
    [ProducesResponseType<ResumenPlan>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearAsync(AltaDePlan alta, CancellationToken ct)
    {
        var resultado = await crearPlan.EjecutarAsync(alta, ct);

        if (!resultado.Correcto)
        {
            // Todo lo que rechaza `CrearPlan` es dato mal capturado, asi que 400 siempre:
            // no hay aqui el tercer desenlace del alta de empresas, donde el aprovisionamiento
            // puede romperse a medio camino y dejar algo reintentable.
            return Problem(
                title: "Plan rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var plan = resultado.Plan!;

        return Created($"/api/plataforma/planes/{plan.Codigo}", plan);
    }

    [HttpPatch("{codigo}/activo")]
    [EndpointName("CambiarActivoDePlan")]
    [EndpointSummary("Retira o reactiva un plan. No afecta a quien ya lo tiene contratado.")]
    [ProducesResponseType<ResumenPlan>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarActivoAsync(
        string codigo, CambioDeActivo cambio, CancellationToken ct)
    {
        var plan = await catalogo.CambiarActivoAsync(
            FormatoCodigoPlan.Normalizar(codigo), cambio.Activo, ct);

        return plan is null
            ? Problem(
                title: "Plan no encontrado",
                detail: $"No existe un plan con el codigo '{codigo}'.",
                statusCode: StatusCodes.Status404NotFound)
            : Ok(plan);
    }
}

/// <summary>
/// El cuerpo del PATCH. Un objeto y no un booleano suelto para que la peticion sea
/// autoexplicativa en un log y para poder agregarle campos sin cambiar la firma.
/// </summary>
public readonly record struct CambioDeActivo(bool Activo);
