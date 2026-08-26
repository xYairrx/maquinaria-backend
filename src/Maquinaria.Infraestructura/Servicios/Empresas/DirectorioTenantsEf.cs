using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maquinaria.Infraestructura.Empresas;

/// <summary>
/// Resuelve empresas contra la base central y las cachea.
///
/// Una resolucion cuesta tres lecturas —tenant, sus limites, y los modulos del plan
/// de su suscripcion vigente—, asi que hacerla en cada peticion serian tres consultas
/// extra contra una base que ni siquiera es la del cliente.
/// </summary>
internal sealed class DirectorioTenantsEf(
    ContextoCentral central,
    IMemoryCache cache,
    IOptions<OpcionesMultiTenancy> opciones,
    ILogger<DirectorioTenantsEf> log) : IDirectorioTenants
{
    public async Task<TenantResuelto?> BuscarPorSlugAsync(string slug, CancellationToken ct)
    {
        var normalizado = slug.Trim().ToLowerInvariant();

        return await ObtenerAsync(
            $"tenant:slug:{normalizado}",
            () => central.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == normalizado && t.EliminadoEn == null, ct),
            ct);
    }

    public async Task<TenantResuelto?> BuscarPorIdAsync(Guid tenantId, CancellationToken ct)
        => await ObtenerAsync(
            $"tenant:id:{tenantId}",
            () => central.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantId && t.EliminadoEn == null, ct),
            ct);

    public void Invalidar(Guid tenantId, string slug)
    {
        cache.Remove($"tenant:id:{tenantId}");
        cache.Remove($"tenant:slug:{slug.Trim().ToLowerInvariant()}");

        log.LogInformation("Cache de tenant invalidada para {Slug}.", slug);
    }

    private async Task<TenantResuelto?> ObtenerAsync(
        string llave,
        Func<Task<Dominio.Plataforma.Tenant?>> consulta,
        CancellationToken ct)
    {
        if (cache.TryGetValue(llave, out TenantResuelto? enCache))
        {
            return enCache;
        }

        var tenant = await consulta();

        if (tenant is null)
        {
            // Los inexistentes NO se cachean. Cachear ausencias abriria un camino para
            // llenar la memoria del servidor pidiendo slugs al azar.
            return null;
        }

        var limites = await central.TenantLimites
            .AsNoTracking()
            .Where(l => l.TenantId == tenant.Id)
            .Join(central.TiposLimite, l => l.TipoLimiteId, t => t.Id,
                (l, t) => new { t.Clave, l.Valor })
            .ToDictionaryAsync(x => x.Clave, x => x.Valor, ct);

        // Los modulos salen del plan de la suscripcion VIGENTE. El constraint EXCLUDE
        // de suscripcion garantiza que no haya dos, asi que este First no es ambiguo.
        var modulos = await central.Suscripciones
            .AsNoTracking()
            .Where(s => s.TenantId == tenant.Id
                && (s.Estado == Dominio.Plataforma.EstadoSuscripcion.Prueba
                    || s.Estado == Dominio.Plataforma.EstadoSuscripcion.Activa))
            .Join(central.PlanModulos, s => s.PlanId, pm => pm.PlanId, (s, pm) => pm.ModuloId)
            .Join(central.Modulos.Where(m => m.Activo), id => id, m => m.Id, (id, m) => m.Clave)
            .ToListAsync(ct);

        var resuelto = new TenantResuelto(
            tenant.Id,
            tenant.Slug,
            tenant.NombreBd,
            tenant.RazonSocial,
            tenant.Estado,
            tenant.EstadoAprovisionamiento,
            tenant.ZonaHoraria,
            tenant.Moneda,
            modulos.ToHashSet(StringComparer.Ordinal),
            limites);

        var vigencia = TimeSpan.FromSeconds(opciones.Value.SegundosCacheTenant);

        // Se cachea bajo LAS DOS llaves: quien entra por slug en el login y quien entra
        // por id en las peticiones siguientes tienen que ver lo mismo, y una sola
        // invalidacion tiene que alcanzar a ambas.
        cache.Set($"tenant:id:{tenant.Id}", resuelto, vigencia);
        cache.Set($"tenant:slug:{tenant.Slug}", resuelto, vigencia);

        log.LogDebug(
            "Tenant {Slug} resuelto: {Modulos} modulos, {Limites} limites propios.",
            tenant.Slug, modulos.Count, limites.Count);

        return resuelto;
    }
}
