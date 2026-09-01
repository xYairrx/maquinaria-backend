using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maquinaria.Infraestructura.Empresas;

internal sealed class RegistroTenantsEf(ContextoCentral central) : IRegistroTenants
{
    public Task<bool> ExisteSlugAsync(string slug, CancellationToken ct)
        // Sin filtrar por eliminado_en: el UNIQUE de slug es global, asi que un slug de
        // una empresa dada de baja sigue ocupado. Y debe seguirlo estando — su base
        // existe y su historial tambien.
        => central.Tenants.AnyAsync(t => t.Slug == slug, ct);

    public Task<Plan?> BuscarPlanPorCodigoAsync(string codigo, CancellationToken ct)
        => central.Planes.FirstOrDefaultAsync(p => p.Codigo == codigo, ct);

    public async Task CrearAsync(Tenant tenant, Suscripcion suscripcion, CancellationToken ct)
    {
        central.Tenants.Add(tenant);
        central.Suscripciones.Add(suscripcion);

        // UN SOLO SaveChanges: EF los envuelve en una transaccion, asi que o entran los
        // dos o ninguno. Un tenant sin suscripcion no tendria ningun modulo contratado.
        await central.SaveChangesAsync(ct);
    }

    public async Task CambiarEstadoAprovisionamientoAsync(
        Guid tenantId, EstadoAprovisionamiento estado, CancellationToken ct)
        => await central.Tenants
            .Where(t => t.Id == tenantId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.EstadoAprovisionamiento, estado)
                      .SetProperty(t => t.ActualizadoEn, DateTime.UtcNow),
                ct);

    public async Task<ResumenEmpresa?> CambiarEstadoAsync(
        string slug, EstadoTenant estado, CancellationToken ct)
    {
        // CON SEGUIMIENTO Y SaveChanges, no ExecuteUpdateAsync como sus vecinas de aqui
        // abajo, y es deliberado: suspender o cancelar a un cliente es de las decisiones
        // mas privilegiadas del sistema, y el interceptor de auditoria SOLO VE lo que pasa
        // por SaveChanges. Con la version rapida el cambio no quedaria en ninguna bitacora.
        //
        // Las de aprovisionamiento se quedan como estan porque las mueve el sistema, no una
        // persona, y su rastro es el log del alta.
        var tenant = await central.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

        if (tenant is null)
        {
            return null;
        }

        // Si el estado ya era ese, EF no marca la entidad como modificada y el interceptor
        // no escribe nada. Un PATCH repetido es un no-op de verdad, no una fila de bitacora
        // que dice que no cambio nada.
        tenant.Estado = estado;
        tenant.ActualizadoEn = DateTime.UtcNow;

        await central.SaveChangesAsync(ct);

        // Se relee de la lista en lugar de armar el resumen a mano: asi la fila que recibe
        // el panel es EXACTAMENTE la misma forma que le llega en el listado, con su plan y
        // su conteo de modulos. Mismo criterio que CambiarActivoAsync con un plan.
        var empresas = await ListarAsync(ct);

        return empresas.FirstOrDefault(e => e.Slug == slug);
    }

    public async Task MarcarListaAsync(Guid tenantId, string versionEsquema, CancellationToken ct)
        => await central.Tenants
            .Where(t => t.Id == tenantId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.EstadoAprovisionamiento, EstadoAprovisionamiento.Lista)
                      .SetProperty(t => t.VersionEsquema, versionEsquema)
                      .SetProperty(t => t.ActualizadoEn, DateTime.UtcNow),
                ct);

    public async Task<IReadOnlyList<ResumenEmpresa>> ListarAsync(CancellationToken ct)
        => await central.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Slug)
            .Select(t => new ResumenEmpresa(
                t.Id,
                t.Slug,
                t.RazonSocial,
                t.Rfc,
                t.Estado,
                t.EstadoAprovisionamiento,
                t.VersionEsquema,

                // Subconsultas y no joins: un tenant sin suscripcion vigente tiene que
                // aparecer en el listado con plan nulo y cero modulos, no desaparecer.
                // Con un join interno se perderia, y son justo los que hay que ver.
                central.Suscripciones
                    .Where(s => s.TenantId == t.Id
                        && (s.Estado == EstadoSuscripcion.Prueba
                            || s.Estado == EstadoSuscripcion.Activa))
                    .Join(central.Planes, s => s.PlanId, p => p.Id, (s, p) => p.Codigo)
                    .FirstOrDefault(),

                central.Suscripciones
                    .Where(s => s.TenantId == t.Id
                        && (s.Estado == EstadoSuscripcion.Prueba
                            || s.Estado == EstadoSuscripcion.Activa))
                    .SelectMany(s => central.PlanModulos.Where(pm => pm.PlanId == s.PlanId))
                    .Count(),

                t.InvitacionEnviada,
                t.CreadoEn))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TenantParaMigrar>> ListarParaMigrarAsync(
        string? slug, CancellationToken ct)
    {
        var consulta = central.Tenants.AsNoTracking().Where(t => t.EliminadoEn == null);

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var normalizado = slug.Trim().ToLowerInvariant();
            consulta = consulta.Where(t => t.Slug == normalizado);
        }

        return await consulta
            .OrderBy(t => t.Slug)
            .Select(t => new TenantParaMigrar(
                t.Id,
                t.Slug,
                t.RazonSocial,
                t.NombreBd,
                t.Estado,
                t.EstadoAprovisionamiento))
            .ToListAsync(ct);
    }

    public Task MarcarInvitacionEnviadaAsync(Guid tenantId, bool enviada, CancellationToken ct)
        => central.Tenants
            .Where(t => t.Id == tenantId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.InvitacionEnviada, enviada), ct);

    public async Task MarcarVersionEsquemaAsync(
        Guid tenantId, string version, CancellationToken ct)
        => await central.Tenants
            .Where(t => t.Id == tenantId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.VersionEsquema, version)
                      .SetProperty(t => t.ActualizadoEn, DateTime.UtcNow),
                ct);

    public Task<Tenant?> BuscarPorSlugAsync(string slug, CancellationToken ct)
        => central.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    /// <summary>23505 es unique_violation en PostgreSQL.</summary>
    public bool EsColisionDeUnicidad(Exception e)
        => e is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } };
}
