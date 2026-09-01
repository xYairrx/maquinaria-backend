using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Plataforma;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maquinaria.Api.Tests;

/// <summary>
/// Que la resolucion de cupos TRADUZCA a SQL.
///
/// Hace falta por la forma que tiene: recorre el catalogo de tipos y a cada uno le cuelga
/// DOS subconsultas correlacionadas contra `tenant_limite` —el valor y el "hay fila"—.
/// Esa es la forma que EF evalua en el cliente cuando se escribe mal, y es exactamente el
/// defecto que costo trece servicios el 2026-08-28. Ahi no truena hasta que la tabla tiene
/// una fila; aqui se quiere saber antes.
///
/// El truco es el mismo de `CatalogoPlanesTraduccionPruebas`: EF traduce ANTES de abrir la
/// conexion, asi que se apunta a un puerto donde no hay nada y se mira QUE excepcion sale.
/// Si es de red, la traduccion funciono.
/// </summary>
public class LimitesTraduccionPruebas
{
    private const string CadenaMuerta =
        "Host=127.0.0.1;Port=1;Database=nada;Username=nadie;Password=x;Timeout=1;";

    [Fact]
    public Task Listar_traduce_sus_dos_subconsultas_correlacionadas()
        => AssertTraduceYSoloFallaLaRed(
            () => Limites().ListarAsync("bajio", CancellationToken.None));

    [Fact]
    public Task Quitar_traduce_su_navegacion_al_tipo()
        // Filtra por `l.TipoLimite!.Clave`, que es una navegacion dentro del Where: la otra
        // forma que EF puede no saber traducir.
        => AssertTraduceYSoloFallaLaRed(
            () => Limites().QuitarAsync("bajio", "max_equipos", CancellationToken.None));

    [Fact]
    public Task El_catalogo_de_tipos_traduce_su_conteo_de_excepciones()
        // Proyecta un `ResumenTipoLimite` con una subconsulta de conteo dentro. Lo que NO
        // entra en el arbol es `EsReconocida`, que se resuelve con un `with` en memoria
        // porque la lista de claves con codigo detras vive en el ensamblado.
        => AssertTraduceYSoloFallaLaRed(
            () => new CatalogoLimitesEf(Contexto()).ListarAsync(CancellationToken.None));

    private static ContextoCentral Contexto()
    {
        var opciones = new DbContextOptionsBuilder<ContextoCentral>();
        opciones.UsarPostgres(CadenaMuerta);

        return new ContextoCentral(opciones.Options);
    }

    private static LimitesTenantEf Limites() => new(Contexto());

    private static async Task AssertTraduceYSoloFallaLaRed(Func<Task> consulta)
    {
        var error = await Record.ExceptionAsync(consulta);

        Assert.NotNull(error);

        var esDeRed = error is NpgsqlException
            || error.InnerException is NpgsqlException
            || error is TimeoutException
            || error.InnerException is TimeoutException;

        Assert.True(
            esDeRed,
            "Se esperaba un fallo de CONEXION, que significa que la consulta si tradujo a "
            + $"SQL. Salio: {error.GetType().Name}: {error.Message}");
    }
}
