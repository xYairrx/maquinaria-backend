using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Maquinaria.Api.TiempoDiseno;

/// <summary>
/// Lo que usa 'dotnet ef' para construir el ContextoCentral.
///
/// Existe por la cadena de conexion: sin fabrica, dotnet ef levanta el host y toma
/// el contexto del contenedor de DI, que esta configurado con ConnectionStrings:Central
/// — la POOLED. El endpoint pooled corre PgBouncer en modo transaccion y no soporta
/// DDL, asi que las migraciones fallarian. Una IDesignTimeDbContextFactory gana sobre
/// el contenedor, y aqui se fuerza ConnectionStrings:Migraciones, la directa.
///
/// Vive en Maquinaria.Api y no en Infraestructura porque EF la busca tambien en el
/// proyecto de arranque, y aqui la configuracion (incluidos los user secrets) se lee
/// igual que en la aplicacion real, sin agregar un solo paquete.
/// </summary>
internal sealed class FabricaContextoCentral : IDesignTimeDbContextFactory<ContextoCentral>
{
    public ContextoCentral CreateDbContext(string[] args)
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

        var opciones = new DbContextOptionsBuilder<ContextoCentral>();
        opciones.UsarPostgres(cadena);

        return new ContextoCentral(opciones.Options);
    }
}
