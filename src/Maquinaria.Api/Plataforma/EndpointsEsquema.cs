using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Api.Plataforma;

/// <summary>
/// Estado del esquema de cada empresa.
///
/// Sin esto el desfase es INVISIBLE hasta que algo truena: una base dos versiones atras
/// funciona bien hasta que alguien usa la pantalla que necesita la tabla nueva.
/// </summary>
internal static class EndpointsEsquema
{
    public static IEndpointRouteBuilder MapearEsquema(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/plataforma/esquema", RevisarAsync)
            .WithTags("Plataforma")
            .RequireAuthorization(PoliticasAutorizacion.Plataforma)
            .WithName("RevisarEsquemaEmpresas")
            .WithSummary("Version de esquema de cada empresa, y quien quedo atrasado.")
            .Produces<RespuestaEsquema>();

        return rutas;
    }

    private static async Task<IResult> RevisarAsync(
        IMigradorEmpresas migrador, CancellationToken ct)
    {
        var estados = await migrador.RevisarAsync(ct);

        var atrasadas = estados.Where(e => !e.AlDia).ToList();

        return Results.Ok(new RespuestaEsquema(
            estados.Count,
            atrasadas.Count,
            estados.FirstOrDefault().VersionEsperada ?? string.Empty,
            estados));
    }
}

/// <param name="Atrasadas">Si no es cero, hay que correr migrar-empresas.</param>
public readonly record struct RespuestaEsquema(
    int Total,
    int Atrasadas,
    string VersionEsperada,
    IReadOnlyList<EstadoEsquema> Empresas);
