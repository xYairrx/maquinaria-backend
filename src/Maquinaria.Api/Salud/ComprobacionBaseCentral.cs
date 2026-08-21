using Maquinaria.Infraestructura.Persistencia;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maquinaria.Api.Salud;

/// <summary>
/// Comprueba que la base central responde.
///
/// Escrita a mano en lugar de traer el paquete de health checks de EF Core: lo unico
/// que hace falta es un CanConnect, y son diez lineas contra una dependencia mas que
/// pinear y auditar.
///
/// Deliberadamente NO consulta ninguna tabla: el chequeo debe medir conectividad, no
/// esquema. Si una tabla falta, eso lo dice el reporte de migraciones, no este.
/// </summary>
internal sealed class ComprobacionBaseCentral(ContextoCentral contexto) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext comprobacion, CancellationToken ct = default)
    {
        try
        {
            return await contexto.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy("La base central responde.")
                : HealthCheckResult.Unhealthy("La base central no acepta conexiones.");
        }
        catch (Exception e)
        {
            // El mensaje de la excepcion NO va al resultado: puede llevar el host y el
            // nombre de la base. Va al log, por el manejador global.
            return HealthCheckResult.Unhealthy("La base central no acepta conexiones.", e);
        }
    }
}
