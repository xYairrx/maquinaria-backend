using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Infraestructura.Empresas;

internal sealed class MigradorEmpresasEf(
    IRegistroTenants registro,
    ProveedorContextoEmpresa proveedor,
    ILogger<MigradorEmpresasEf> log) : IMigradorEmpresas
{
    public async Task<ReporteMigracion> MigrarAsync(string? slug, CancellationToken ct)
    {
        var empresas = await registro.ListarParaMigrarAsync(slug, ct);
        var resultados = new List<ResultadoEmpresa>(empresas.Count);

        log.LogInformation("Migrando {Cuantas} empresas.", empresas.Count);

        foreach (var empresa in empresas)
        {
            ct.ThrowIfCancellationRequested();

            // Una base a medio aprovisionar no se migra: sus tablas podrian no existir y
            // el error saldria como "relation does not exist" en lugar de decir lo que
            // realmente pasa. El alta reintentable es la que tiene que arreglarla.
            if (empresa.Aprovisionamiento != EstadoAprovisionamiento.Lista)
            {
                log.LogWarning(
                    "{Slug} omitida: aprovisionamiento {Estado}.",
                    empresa.Slug, empresa.Aprovisionamiento);

                resultados.Add(new ResultadoEmpresa(
                    empresa.Slug,
                    DesenlaceMigracion.Omitida,
                    null,
                    $"Aprovisionamiento {empresa.Aprovisionamiento}"));

                continue;
            }

            try
            {
                resultados.Add(await MigrarUnaAsync(empresa, ct));
            }
            catch (Exception e)
            {
                // NO SE PROPAGA. Es la razon de ser de este comando: si truena en la
                // empresa 23, las 22 anteriores ya migraron y las siguientes tienen
                // derecho a intentarlo. Abortar dejaria un desfase peor que el inicial.
                log.LogError(e, "Fallo la migracion de {Slug}.", empresa.Slug);

                resultados.Add(new ResultadoEmpresa(
                    empresa.Slug, DesenlaceMigracion.Fallida, null, e.Message));
            }
        }

        var reporte = new ReporteMigracion(resultados);

        log.LogInformation(
            "Migracion terminada. {AlDia} al dia, {Migradas} migradas, "
            + "{Omitidas} omitidas, {Fallidas} fallidas.",
            reporte.AlDia, reporte.Migradas, reporte.Omitidas, reporte.Fallidas);

        return reporte;
    }

    private async Task<ResultadoEmpresa> MigrarUnaAsync(
        TenantParaMigrar empresa, CancellationToken ct)
    {
        await using var contexto = proveedor.ParaMigrar(empresa.NombreBd);

        var pendientes = (await contexto.Database.GetPendingMigrationsAsync(ct)).ToList();

        if (pendientes.Count == 0)
        {
            // Se registra la version igual: si un alta anterior no la guardo, esta es la
            // oportunidad de corregir el dato sin cambiar nada del esquema.
            var actual = (await contexto.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault();

            if (actual is not null)
            {
                await registro.MarcarVersionEsquemaAsync(empresa.Id, actual, ct);
            }

            return new ResultadoEmpresa(empresa.Slug, DesenlaceMigracion.AlDia, actual, null);
        }

        log.LogInformation(
            "{Slug}: aplicando {Cuantas} migraciones.", empresa.Slug, pendientes.Count);

        await contexto.Database.MigrateAsync(ct);

        var version = (await contexto.Database.GetAppliedMigrationsAsync(ct)).Last();

        // El registro de la version va DESPUES de migrar y en la base CENTRAL. Si esto
        // fallara, el esquema quedaria bien y el dato mal — que es el fallo menos malo de
        // los dos, y el que RevisarAsync detecta comparando contra la base real.
        await registro.MarcarVersionEsquemaAsync(empresa.Id, version, ct);

        return new ResultadoEmpresa(
            empresa.Slug,
            DesenlaceMigracion.Migrada,
            version,
            $"{pendientes.Count} migraciones");
    }

    public async Task<IReadOnlyList<EstadoEsquema>> RevisarAsync(CancellationToken ct)
    {
        var empresas = await registro.ListarParaMigrarAsync(null, ct);
        var estados = new List<EstadoEsquema>(empresas.Count);

        // La ultima migracion que trae el ENSAMBLADO: es la version a la que todas
        // deberian llegar.
        string esperada;

        await using (var referencia = proveedor.ParaLeerMigraciones())
        {
            esperada = referencia.Database.GetMigrations().Last();
        }

        foreach (var empresa in empresas)
        {
            string? aplicada = null;

            try
            {
                // Se lee de la BASE REAL, no de tenant.version_esquema. Ese campo es una
                // copia y puede estar desactualizado; el historial de migraciones de cada
                // base es la verdad.
                await using var contexto = proveedor.ParaMigrar(empresa.NombreBd);
                aplicada = (await contexto.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault();
            }
            catch (Exception e)
            {
                log.LogWarning(e, "No se pudo leer el esquema de {Slug}.", empresa.Slug);
            }

            estados.Add(new EstadoEsquema(
                empresa.Slug,
                aplicada,
                esperada,
                aplicada == esperada,
                empresa.Aprovisionamiento));
        }

        return estados;
    }
}
