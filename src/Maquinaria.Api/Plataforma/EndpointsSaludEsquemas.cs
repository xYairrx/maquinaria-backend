using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Api.Plataforma;

/// <summary>
/// Salud de esquemas: que version tiene cada empresa y quien quedo atrasado.
///
/// Existe porque las migraciones de empresa se aplican N veces, una por base, y un fallo
/// parcial deja versiones desalineadas. Sin este reporte el desfase es invisible hasta que
/// algo truena — y ya paso: `demo` y `bajio` quedaron una migracion atras de la plantilla
/// sin que nada lo dijera.
///
/// Solo la plataforma, misma policy que el resto del panel de superadministracion: el
/// estado de las bases de todos los clientes no es asunto de ningun cliente.
/// </summary>
internal static class EndpointsSaludEsquemas
{
    public static IEndpointRouteBuilder MapearSaludEsquemas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/plataforma/salud")
            .WithTags("Plataforma")
            .RequireAuthorization(PoliticasAutorizacion.Plataforma);

        grupo.MapGet("/esquemas", EsquemasAsync)
            .WithName("SaludDeEsquemas")
            .WithSummary("Version de esquema por empresa, y quien quedo atrasado.")
            .Produces<ReporteSaludEsquemas>();

        return rutas;
    }

    private static async Task<IResult> EsquemasAsync(SaludEsquemas caso, CancellationToken ct)
        => Results.Ok(await caso.EjecutarAsync(ct));
}
