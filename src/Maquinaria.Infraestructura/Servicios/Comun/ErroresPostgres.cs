using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maquinaria.Infraestructura.Servicios.Comun;

/// <summary>
/// Reconoce los rechazos del motor que son REGLA DE NEGOCIO y no fallo del sistema.
///
/// Por que existe: las garantias importantes de este esquema las impone Postgres, no el
/// codigo —el <c>UNIQUE</c> de un codigo de catalogo, el <c>EXCLUDE</c> de no-traslape de
/// <c>ocupacion_equipo</c>—. Cuando el motor rechaza, EF envuelve la excepcion de Npgsql en
/// una <see cref="DbUpdateException"/>, y sin traducirla el usuario ve un 500 con
/// «An error occurred while saving the entity changes» y el log una traza de Npgsql.
///
/// LA COMPROBACION PREVIA NO SUSTITUYE A ESTO. Un <c>if (ya existe)</c> antes del INSERT da
/// un mensaje mejor, pero bajo concurrencia las dos transacciones leen «no existe» y las dos
/// insertan: la que pierde llega aqui. Las dos cosas son necesarias y ninguna sobra.
/// </summary>
internal static class ErroresPostgres
{
    /// <summary>Violacion de UNIQUE o de PRIMARY KEY.</summary>
    public const string Unico = "23505";

    /// <summary>Violacion de un EXCLUDE. Es el del no-traslape de ocupacion_equipo.</summary>
    public const string Traslape = "23P01";

    /// <summary>Violacion de CHECK.</summary>
    public const string Check = "23514";

    /// <summary>
    /// Violacion de llave foranea. Cubre las dos direcciones: referenciar algo que no existe,
    /// y borrar algo que alguien referencia con RESTRICT.
    /// </summary>
    public const string Foranea = "23503";

    /// <summary>
    /// <c>raise_exception</c>: lo que emite un <c>RAISE EXCEPTION</c> de un trigger. Es el
    /// codigo de las tres garantias que este esquema impone con triggers y no con CHECK —el
    /// contrato inmutable, la auditoria append-only, el rol de sistema— porque cruzan tablas o
    /// comparan el valor anterior con el nuevo, y un CHECK no puede hacer ni una ni otra.
    /// </summary>
    public const string Excepcion = "P0001";

    /// <summary>
    /// El <c>SqlState</c> del rechazo, o nulo si la excepcion no viene del motor —una
    /// conexion caida, un timeout— y por tanto NO es una regla de negocio.
    /// </summary>
    public static string? Estado(this DbUpdateException excepcion)
        => (excepcion.InnerException as PostgresException)?.SqlState;

    public static bool EsViolacionDeUnico(this DbUpdateException excepcion)
        => excepcion.Estado() == Unico;

    public static bool EsTraslape(this DbUpdateException excepcion)
        => excepcion.Estado() == Traslape;
}
