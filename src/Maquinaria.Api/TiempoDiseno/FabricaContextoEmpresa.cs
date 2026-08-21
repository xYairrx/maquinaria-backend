using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Maquinaria.Api.TiempoDiseno;

/// <summary>
/// Lo que usa 'dotnet ef' para construir el ContextoEmpresa.
///
/// Aqui la fabrica es INDISPENSABLE, no una correccion como en el caso central:
/// ContextoEmpresa no tiene cadena de conexion fija —se resuelve por peticion— asi
/// que EF no tiene de donde sacarla.
///
/// APUNTA A maquinaria_plantilla, UNA BASE VACIA, Y ESO ES EL PUNTO. En cuanto
/// existe esta fabrica, un 'dotnet ef database update --context ContextoEmpresa'
/// distraido aplicaria migraciones a la base a la que apunte: la central, o peor,
/// la de un cliente, fuera del proceso controlado de migrar-empresas. Apuntando a
/// una base vacia ese comando no puede hacer dano, y de paso da donde inspeccionar
/// el esquema de empresa generado.
///
/// NO lleva cadena propia: toma ConnectionStrings:Migraciones y le sustituye el
/// Database. Asi no hay un tercer secreto que se desincronice, y el camino de
/// codigo es el mismo que usara el runtime con cada empresa.
/// </summary>
internal sealed class FabricaContextoEmpresa : IDesignTimeDbContextFactory<ContextoEmpresa>
{
    private const string BasePlantilla = "maquinaria_plantilla";

    public ContextoEmpresa CreateDbContext(string[] args)
    {
        var configuracion = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cadena = configuracion.GetConnectionString("Migraciones")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:Migraciones. Es la cadena SIN -pooler: el "
                + "endpoint pooled corre PgBouncer en modo transaccion y no admite DDL.");

        // NpgsqlConnectionStringBuilder y no un reemplazo de texto: la cadena de
        // Neon trae SSL Mode y Channel Binding, y armarla a mano perderia alguno.
        var constructor = new NpgsqlConnectionStringBuilder(cadena)
        {
            Database = BasePlantilla,
        };

        var opciones = new DbContextOptionsBuilder<ContextoEmpresa>();
        opciones.UsarPostgres(constructor.ConnectionString);

        return new ContextoEmpresa(opciones.Options);
    }
}
