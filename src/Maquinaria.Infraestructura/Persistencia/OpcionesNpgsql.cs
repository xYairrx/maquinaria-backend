using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Persistencia;

/// <summary>
/// El unico lugar donde se configura el proveedor de base de datos.
///
/// Existe para que la aplicacion y el tiempo de diseno no puedan divergir: si en
/// Program.cs se configurara UseSnakeCaseNamingConvention y en la fabrica no, la
/// migracion generada describiria un esquema distinto del que espera la
/// aplicacion en ejecucion, y el error saldria hasta la primera consulta.
/// </summary>
public static class OpcionesNpgsql
{
    public static DbContextOptionsBuilder UsarPostgres(
        this DbContextOptionsBuilder opciones,
        string cadena)
        => opciones
            .UseNpgsql(cadena)
            .UseSnakeCaseNamingConvention();
}
