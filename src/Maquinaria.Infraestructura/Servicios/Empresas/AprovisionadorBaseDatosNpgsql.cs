using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Maquinaria.Infraestructura.Empresas;

/// <summary>
/// Crea y migra la base de una empresa.
/// </summary>
internal sealed class AprovisionadorBaseDatosNpgsql(
    FabricaConexionesEmpresa fabrica,
    ProveedorContextoEmpresa proveedor,
    ILogger<AprovisionadorBaseDatosNpgsql> log) : IAprovisionadorBaseDatos
{
    public async Task<bool> ExisteBaseAsync(string nombreBd, CancellationToken ct)
    {
        FabricaConexionesEmpresa.ValidarNombreBd(nombreBd);

        await using var conexion = new NpgsqlConnection(fabrica.CadenaCentralDirecta());
        await conexion.OpenAsync(ct);

        // AQUI SI se parametriza: datname se compara como VALOR, no se usa como
        // identificador. Es la diferencia con el CREATE DATABASE de abajo.
        await using var comando = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @nombre", conexion);

        comando.Parameters.AddWithValue("nombre", nombreBd);

        return await comando.ExecuteScalarAsync(ct) is not null;
    }

    public async Task CrearBaseAsync(string nombreBd, CancellationToken ct)
    {
        // Se revalida aunque el llamador ya lo haya hecho. Es la ultima linea antes de
        // concatenar un identificador en DDL, y una validacion que depende de que el
        // llamador se acuerde no es un control de seguridad.
        FabricaConexionesEmpresa.ValidarNombreBd(nombreBd);

        // CONEXION DIRECTA, no la pooled: el endpoint pooled de Neon corre PgBouncer en
        // modo transaccion y no admite DDL.
        await using var conexion = new NpgsqlConnection(fabrica.CadenaCentralDirecta());
        await conexion.OpenAsync(ct);

        // SIN transaccion explicita y sin entrecomillar el identificador:
        //
        // - PostgreSQL NO permite CREATE DATABASE dentro de una transaccion. Por eso se
        //   usa Npgsql directo y no EF Core, que envuelve todo en una.
        // - El identificador se CONCATENA porque SQL no permite parametrizarlo. De ahi
        //   que el formato se valide arriba con regex y que el CHECK de tenant.nombre_bd
        //   sea control de seguridad y no cosmetica.
        // - Sin comillas porque el formato ya garantiza minusculas, digitos y guiones
        //   bajos, que es justo la razon de usar guiones bajos y no guiones: un nombre
        //   entrecomillado habria que entrecomillarlo en cada sentencia posterior.
        await using var comando = new NpgsqlCommand($"CREATE DATABASE {nombreBd}", conexion);

        await comando.ExecuteNonQueryAsync(ct);

        log.LogInformation("Base {NombreBd} creada.", nombreBd);
    }

    public async Task<string> MigrarAsync(string nombreBd, CancellationToken ct)
    {
        await using var contexto = proveedor.ParaMigrar(nombreBd);

        // Esto aplica las migraciones Y siembra permisos y roles, porque las semillas
        // viven dentro de migraciones. No hace falta codigo de semilla aparte.
        await contexto.Database.MigrateAsync(ct);

        var aplicadas = await contexto.Database.GetAppliedMigrationsAsync(ct);
        var ultima = aplicadas.LastOrDefault()
            ?? throw new InvalidOperationException(
                $"La base {nombreBd} quedo sin ninguna migracion aplicada.");

        log.LogInformation("Base {NombreBd} migrada a {Version}.", nombreBd, ultima);

        return ultima;
    }

    public async Task<string?> VersionAplicadaAsync(string nombreBd, CancellationToken ct)
    {
        await using var contexto = proveedor.ParaMigrar(nombreBd);

        // Devuelve vacio si la base existe y no tiene historial; si la base NO existe,
        // esto revienta con error de conexion. Por eso el llamador pregunta primero por
        // ExisteBaseAsync.
        var aplicadas = await contexto.Database.GetAppliedMigrationsAsync(ct);

        return aplicadas.LastOrDefault();
    }

    public IReadOnlyList<string> VersionesDisponibles()
    {
        using var contexto = proveedor.ParaLeerMigraciones();

        // GetMigrations lee el ENSAMBLADO, no la base: no abre ninguna conexion, asi que
        // esto no cuesta ni un viaje a Neon.
        return [.. contexto.Database.GetMigrations()];
    }
}
