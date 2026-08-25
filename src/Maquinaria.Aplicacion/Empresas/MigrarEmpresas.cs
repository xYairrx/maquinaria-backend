using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Aplica las migraciones pendientes de ContextoEmpresa a TODAS las bases de empresa
/// registradas, una por una, y devuelve el resultado de cada una.
///
/// RESISTENTE A FALLOS PARCIALES, que es la razon de existir: las migraciones de empresa
/// se corren N veces —una por base— y si truena en la empresa 23 las 22 anteriores ya
/// quedaron migradas. Que una falle NO detiene a las demas, y el reporte dice quien quedo
/// atras. Sin eso, el desfase es invisible hasta que algo revienta en produccion.
///
/// No hay transaccion que abarque varias bases, y no puede haberla: son bases distintas.
/// Lo que hace manejable el fallo parcial es que cada base lleva su propia
/// __EFMigrationsHistory y que el historial es append-only, asi que la que quedo atras
/// alcanza en la siguiente corrida.
/// </summary>
public sealed class MigrarEmpresas(
    IRegistroTenants registro,
    IAprovisionadorBaseDatos bases,
    ILogger<MigrarEmpresas> log)
{
    public async Task<ReporteMigracion> EjecutarAsync(CancellationToken ct)
    {
        var disponibles = bases.VersionesDisponibles();
        var empresas = await registro.ListarConEsquemaAsync(ct);

        log.LogInformation(
            "migrar-empresas: {Empresas} empresas, version disponible {Version}.",
            empresas.Count, disponibles.Count > 0 ? disponibles[^1] : "(ninguna)");

        var resultados = new List<ResultadoMigracionEmpresa>(empresas.Count);

        foreach (var empresa in empresas)
        {
            resultados.Add(await MigrarUnaAsync(empresa, ct));
        }

        return new ReporteMigracion(
            disponibles.Count > 0 ? disponibles[^1] : null, resultados);
    }

    private async Task<ResultadoMigracionEmpresa> MigrarUnaAsync(
        EmpresaConEsquema empresa, CancellationToken ct)
    {
        try
        {
            if (!await bases.ExisteBaseAsync(empresa.NombreBd, ct))
            {
                // No es un fallo de la migracion: es un alta que no llego a crear la base.
                // Se reporta y NO cuenta para el codigo de salida, o un tenant roto haria
                // fallar el comando para siempre.
                return new ResultadoMigracionEmpresa(
                    empresa.Slug, empresa.VersionEsquema, null, EstadoMigracion.Omitida,
                    "Su base de datos no existe. Reintenta el aprovisionamiento, no la migracion.");
            }

            // La version ANTES se lee de la base y no de tenant.version_esquema: la base es
            // la verdad, y si la central quedo desincronizada —porque alguien aplico a
            // mano— este comando es justo el que tiene que corregirla.
            var antes = await bases.VersionAplicadaAsync(empresa.NombreBd, ct);
            var despues = await bases.MigrarAsync(empresa.NombreBd, ct);

            if (!string.Equals(despues, empresa.VersionEsquema, StringComparison.Ordinal))
            {
                await registro.ActualizarVersionEsquemaAsync(empresa.Id, despues, ct);
            }

            return new ResultadoMigracionEmpresa(
                empresa.Slug, antes, despues, EstadoMigracion.Migrada, null);
        }
        catch (Exception e)
        {
            log.LogError(e, "Fallo la migracion de la empresa {Slug}.", empresa.Slug);

            // El motivo SI incluye el mensaje de la excepcion, al contrario que las
            // respuestas HTTP: esto lo lee el operador en su terminal, y sin el motivo el
            // reporte solo dice que algo se rompio.
            return new ResultadoMigracionEmpresa(
                empresa.Slug, empresa.VersionEsquema, null, EstadoMigracion.Fallida, e.Message);
        }
    }
}

/// <summary>
/// Tres desenlaces, no dos. Mismo criterio que <c>ResultadoAlta</c>: lo que no se pudo
/// intentar no debe verse igual que lo que se intento y trono.
/// </summary>
public enum EstadoMigracion
{
    /// <summary>Se aplico lo que faltaba. Si no faltaba nada, tambien: migrar es idempotente.</summary>
    Migrada = 1,

    /// <summary>No habia base que migrar.</summary>
    Omitida = 2,

    Fallida = 3,
}

public sealed record ResultadoMigracionEmpresa(
    string Slug,
    string? VersionAntes,
    string? VersionDespues,
    EstadoMigracion Estado,
    string? Motivo);

public sealed record ReporteMigracion(
    string? VersionDisponible,
    IReadOnlyList<ResultadoMigracionEmpresa> Empresas)
{
    public int Migradas => Empresas.Count(e => e.Estado == EstadoMigracion.Migrada);

    public int Omitidas => Empresas.Count(e => e.Estado == EstadoMigracion.Omitida);

    public int Fallidas => Empresas.Count(e => e.Estado == EstadoMigracion.Fallida);

    /// <summary>Lo que decide el codigo de salida del comando.</summary>
    public bool HayFallas => Fallidas > 0;
}
