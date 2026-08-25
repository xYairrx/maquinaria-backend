using Maquinaria.Aplicacion.Plataforma;

namespace Maquinaria.Api.Plataforma;

/// <summary>
/// El catalogo comercial: los planes y los modulos que los definen. Solo la plataforma.
///
/// NO HAY PUT NI PATCH DE UN PLAN ENTERO, y es una decision, no una omision. `Suscripcion`
/// no guarda importe —solo apunta al plan— y el plan ES su conjunto de modulos, asi que
/// editar el precio reescribiria lo que pagaron los suscriptores historicos y editar los
/// modulos cambiaria el acceso de los actuales, retroactivamente. Lo que si se puede es
/// retirar un plan y crear su sucesor, que es lo que el modelo contempla con `activo`.
/// El razonamiento largo esta en `CrearPlan`.
/// </summary>
internal static class EndpointsPlanes
{
    public static IEndpointRouteBuilder MapearPlanes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/plataforma")
            .WithTags("Plataforma")
            .RequireAuthorization(PoliticasAutorizacion.Plataforma);

        grupo.MapGet("/planes", ListarPlanesAsync)
            .WithName("ListarPlanes")
            .WithSummary("Los planes del catalogo, activos e inactivos, con sus modulos.")
            .Produces<IReadOnlyList<ResumenPlan>>();

        grupo.MapPost("/planes", CrearAsync)
            .WithName("CrearPlan")
            .WithSummary("Crea un plan con su conjunto de modulos.")
            .Produces<ResumenPlan>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        grupo.MapPatch("/planes/{codigo}/activo", CambiarActivoAsync)
            .WithName("CambiarActivoDePlan")
            .WithSummary("Retira o reactiva un plan. No afecta a quien ya lo tiene contratado.")
            .Produces<ResumenPlan>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapGet("/modulos", ListarModulosAsync)
            .WithName("ListarModulos")
            .WithSummary("El catalogo de modulos activos, para armar un plan.")
            .Produces<IReadOnlyList<ResumenModulo>>();

        return rutas;
    }

    private static async Task<IResult> ListarPlanesAsync(
        ICatalogoPlanes catalogo, CancellationToken ct)
        => Results.Ok(await catalogo.ListarPlanesAsync(ct));

    private static async Task<IResult> ListarModulosAsync(
        ICatalogoPlanes catalogo, CancellationToken ct)
        => Results.Ok(await catalogo.ListarModulosAsync(ct));

    private static async Task<IResult> CrearAsync(
        AltaDePlan alta, CrearPlan caso, CancellationToken ct)
    {
        var resultado = await caso.EjecutarAsync(alta, ct);

        if (!resultado.Correcto)
        {
            // Todo lo que rechaza `CrearPlan` es dato mal capturado, asi que 400 siempre:
            // no hay aqui el tercer desenlace del alta de empresas, donde el aprovisionamiento
            // puede romperse a medio camino y dejar algo reintentable.
            return Results.Problem(
                title: "Plan rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
        }

        var plan = resultado.Plan!;

        return Results.Created($"/api/plataforma/planes/{plan.Codigo}", plan);
    }

    private static async Task<IResult> CambiarActivoAsync(
        string codigo, CambioDeActivo cambio, ICatalogoPlanes catalogo, CancellationToken ct)
    {
        var plan = await catalogo.CambiarActivoAsync(
            FormatoCodigoPlan.Normalizar(codigo), cambio.Activo, ct);

        return plan is null
            ? Results.Problem(
                title: "Plan no encontrado",
                detail: $"No existe un plan con el codigo '{codigo}'.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(plan);
    }
}

/// <summary>
/// El cuerpo del PATCH. Un objeto y no un booleano suelto para que la peticion sea
/// autoexplicativa en un log y para poder agregarle campos sin cambiar la firma.
/// </summary>
internal readonly record struct CambioDeActivo(bool Activo);
