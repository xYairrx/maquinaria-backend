using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Plataforma;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maquinaria.Api.Tests;

/// <summary>
/// Que las consultas del catalogo de planes TRADUZCAN a SQL.
///
/// POR QUE ESTO HACE FALTA: una proyeccion de LINQ que EF no sabe convertir no falla al
/// compilar. Falla en tiempo de ejecucion, la primera vez que alguien abre la pantalla, con
/// un `InvalidOperationException` que dice "could not be translated". Y la de
/// `ListarPlanesAsync` tiene las dos formas que mas veces se rompen: una subconsulta
/// correlacionada —contar suscripciones por plan— y una navegacion a traves de otra
/// navegacion —`pm.Modulo!.Activo` dentro del Select—.
///
/// COMO SE COMPRUEBA SIN BASE DE DATOS: EF traduce ANTES de abrir la conexion. Asi que se
/// apunta a un puerto donde no hay nada y se mira QUE excepcion sale. Si es de red, la
/// traduccion funciono y el fallo es solo que no hay servidor. Si es de traduccion, nunca
/// llego a intentar conectarse.
///
/// Es un truco, y por eso va explicado: la alternativa era exponer los IQueryable solo para
/// poder llamarles ToQueryString(), que es cambiar el codigo de produccion para que la
/// prueba sea mas bonita.
/// </summary>
public class CatalogoPlanesTraduccionPruebas
{
    /// <summary>Puerto sin nada escuchando. Nunca se conecta; solo hace falta la forma.</summary>
    private const string CadenaMuerta =
        "Host=127.0.0.1;Port=1;Database=nada;Username=nadie;Password=x;Timeout=1;";

    private static CatalogoPlanesEf Catalogo()
    {
        var opciones = new DbContextOptionsBuilder<ContextoCentral>();
        opciones.UsarPostgres(CadenaMuerta);

        return new CatalogoPlanesEf(new ContextoCentral(opciones.Options));
    }

    private static async Task AssertTraduceYSoloFallaLaRed(Func<Task> consulta)
    {
        var error = await Record.ExceptionAsync(consulta);

        Assert.NotNull(error);

        // Lo que se busca: que el fallo sea de CONEXION. Npgsql lanza NpgsqlException —o
        // la envuelve— cuando no encuentra servidor.
        var esDeRed = error is NpgsqlException
            || error.InnerException is NpgsqlException
            || error is TimeoutException
            || error.InnerException is TimeoutException
            || error is System.Net.Sockets.SocketException
            || error.InnerException is System.Net.Sockets.SocketException;

        Assert.True(
            esDeRed,
            $"Se esperaba un fallo de conexion, que significa que la consulta SI tradujo a "
            + $"SQL. Salio {error.GetType().Name}: {error.Message}");
    }

    [Fact]
    public async Task La_lista_de_planes_traduce_a_SQL()
    {
        // La mas expuesta: subconsulta correlacionada de suscripciones + navegacion anidada
        // a modulo dentro de la proyeccion.
        await AssertTraduceYSoloFallaLaRed(
            () => Catalogo().ListarPlanesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task La_lista_de_modulos_traduce_a_SQL()
        => await AssertTraduceYSoloFallaLaRed(
            () => Catalogo().ListarModulosAsync(CancellationToken.None));

    [Fact]
    public async Task La_comprobacion_de_codigo_repetido_traduce_a_SQL()
        => await AssertTraduceYSoloFallaLaRed(
            () => Catalogo().ExisteCodigoAsync("base", CancellationToken.None));

    [Fact]
    public async Task La_busqueda_de_claves_desconocidas_traduce_a_SQL()
        // Lleva un `Contains` sobre una lista en memoria, que EF convierte en un `IN`.
        => await AssertTraduceYSoloFallaLaRed(
            () => Catalogo().ClavesDeModuloDesconocidasAsync(
                ["equipos", "rentas"], CancellationToken.None));

    [Fact]
    public async Task El_cambio_de_activo_traduce_a_SQL()
        // `ExecuteUpdateAsync` se traduce distinto que un SaveChanges: es un UPDATE directo
        // y no pasa por el rastreador de cambios.
        => await AssertTraduceYSoloFallaLaRed(
            () => Catalogo().CambiarActivoAsync("base", false, CancellationToken.None));
}
