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

    /// <summary>
    /// La FORMA DE LA RESPUESTA ES CONTRATO: el frontend ya la consume en dos pantallas,
    /// asi que ningun nombre de campo se toca. Por eso hay una proyeccion en lugar de
    /// devolver lo que da el migrador tal cual.
    ///
    /// El migrador ya entrega la version disponible aparte de la lista, asi que aqui no se
    /// deduce de ninguna fila: un sistema sin empresas sigue reportando a que version lleva
    /// este binario.
    /// </summary>
    private static async Task<IResult> EsquemasAsync(
        IMigradorEmpresas migrador, CancellationToken ct)
    {
        // Lee el historial de CADA BASE, no la copia de tenant.version_esquema: ese campo
        // puede haber quedado atras si alguien aplico migraciones por fuera, y este
        // endpoint existe justo para encontrar ese desajuste.
        var reporte = await migrador.RevisarAsync(ct);

        var empresas = reporte.Empresas
            .Select(e => new EstadoEsquemaEmpresa(
                e.Id,
                e.Slug,
                e.RazonSocial,
                e.Estado,
                e.Aprovisionamiento,
                e.VersionAplicada,
                e.MigracionesPendientes,
                e.Desfasada,
                e.VersionReconocida))
            .ToList();

        return Results.Ok(new ReporteSaludEsquemas(
            // Nula solo si el ensamblado no trae migraciones. El frontend ya la tipa como
            // nulo posible.
            reporte.VersionDisponible,
            empresas.Count,

            // El conteo va calculado y no lo saca la pantalla recorriendo la lista: es el
            // numero que decide si se muestra la alerta.
            empresas.Count(e => e.Desfasada),
            empresas));
    }
}
