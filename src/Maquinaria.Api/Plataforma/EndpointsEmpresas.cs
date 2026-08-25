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

        grupo.MapPost("/{slug}/reintento", ReintentarAsync)
            .WithName("ReintentarAltaEmpresa")
            .WithSummary("Reintenta un alta que quedo en Fallida. Solo desde ese estado.")
            .Produces<EmpresaAprovisionada>()
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

    /// <summary>
    /// Vuelve a correr los pasos 2 a 6 del aprovisionamiento sobre un tenant en Fallida.
    ///
    /// 200 y no 201: el tenant ya existia antes de esta llamada, asi que no se creo
    /// ningun recurso nuevo. El cuerpo es el mismo <see cref="EmpresaAprovisionada"/> del
    /// alta, porque lo que el panel necesita mostrar es lo mismo.
    /// </summary>
    private static async Task<IResult> ReintentarAsync(
        string slug, ReintentoDeAlta reintento, AprovisionarEmpresa caso, CancellationToken ct)
    {
        var resultado = await caso.ReintentarAsync(slug, reintento, ct);

        if (resultado.Correcto)
        {
            return Results.Ok(resultado.Empresa!.Value);
        }

        // ponytail: los tres rechazos —slug inexistente, estado distinto de Fallida y
        // registro inconsistente— salen todos como 400 con su motivo en el detalle, en
        // lugar de 404 y 409 por separado. Es un endpoint del panel de plataforma, ya
        // autenticado, y lo unico que hace la interfaz con la respuesta es mostrar el
        // texto; distinguir codigos no cambiaria una linea del frontend.
        return resultado.EsRechazo
            ? Results.Problem(
                title: "Reintento rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest)
            : Results.Problem(
                title: "Aprovisionamiento incompleto",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status500InternalServerError);
    }
}
