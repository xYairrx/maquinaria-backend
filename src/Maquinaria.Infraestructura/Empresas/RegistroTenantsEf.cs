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

    public async Task MarcarListaAsync(Guid tenantId, string versionEsquema, CancellationToken ct)
        => await central.Tenants
            .Where(t => t.Id == tenantId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.EstadoAprovisionamiento, EstadoAprovisionamiento.Lista)
                      .SetProperty(t => t.VersionEsquema, versionEsquema)
                      .SetProperty(t => t.ActualizadoEn, DateTime.UtcNow),
                ct);

    public async Task ActualizarVersionEsquemaAsync(
        Guid tenantId, string versionEsquema, CancellationToken ct)
        => await central.Tenants
            .Where(t => t.Id == tenantId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.VersionEsquema, versionEsquema)
                      .SetProperty(t => t.ActualizadoEn, DateTime.UtcNow),
                ct);

    public async Task<IReadOnlyList<EmpresaConEsquema>> ListarConEsquemaAsync(
        CancellationToken ct)
        => await central.Tenants
            .AsNoTracking()
            .Where(t => t.EliminadoEn == null)
            .OrderBy(t => t.Slug)
            .Select(t => new EmpresaConEsquema(
                t.Id,
                t.Slug,
                t.RazonSocial,
                t.NombreBd,
                t.Estado,
                t.EstadoAprovisionamiento,
                t.VersionEsquema))
            .ToListAsync(ct);

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

                t.CreadoEn))
            .ToListAsync(ct);

    public Task<Tenant?> BuscarPorSlugAsync(string slug, CancellationToken ct)
        => central.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    /// <summary>23505 es unique_violation en PostgreSQL.</summary>
    public bool EsColisionDeUnicidad(Exception e)
        => e is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } };
}
