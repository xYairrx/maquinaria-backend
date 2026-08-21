using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Api.Plataforma;

/// <summary>
/// Alta de empresas. Solo la plataforma: aqui se crean bases de datos.
/// </summary>
internal static class EndpointsEmpresas
{
    public static IEndpointRouteBuilder MapearEmpresas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/plataforma/empresas")
            .WithTags("Plataforma")
            .RequireAuthorization(PoliticasAutorizacion.Plataforma);

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarEmpresas")
            .WithSummary("Todas las empresas, con su estado de aprovisionamiento.")
            .Produces<IReadOnlyList<ResumenEmpresa>>();

        grupo.MapPost("/", AltaAsync)
            .WithName("DarDeAltaEmpresa")
            .WithSummary("Da de alta una empresa: crea y migra su base, y invita a su administrador.")
            .Produces<EmpresaAprovisionada>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return rutas;
    }

    private static async Task<IResult> ListarAsync(IRegistroTenants registro, CancellationToken ct)
        => Results.Ok(await registro.ListarAsync(ct));

    private static async Task<IResult> AltaAsync(
        AltaDeEmpresa alta, AprovisionarEmpresa caso, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alta.Slug)
            || string.IsNullOrWhiteSpace(alta.RazonSocial)
            || string.IsNullOrWhiteSpace(alta.CorreoAdministrador)
            || string.IsNullOrWhiteSpace(alta.NombreAdministrador))
        {
            return Results.Problem(
                title: "Datos incompletos",
                detail: "Slug, razon social, y correo y nombre del administrador son obligatorios.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var resultado = await caso.EjecutarAsync(alta, ct);

        if (resultado.Correcto)
        {
            var empresa = resultado.Empresa!.Value;

            return Results.Created($"/api/plataforma/empresas/{empresa.Slug}", empresa);
        }

        // Un rechazo por validacion es 400 y NO es un fallo del sistema: no debe
        // verse igual que un aprovisionamiento roto, ni en la respuesta ni en el log.
        return resultado.EsRechazo
            ? Results.Problem(
                title: "Alta rechazada",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest)
            : Results.Problem(
                title: "Aprovisionamiento incompleto",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status500InternalServerError);
    }
}
