using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Plataforma;

internal sealed class CatalogoPlanesEf(ContextoCentral central) : ICatalogoPlanes
{
    public async Task<IReadOnlyList<ResumenPlan>> ListarPlanesAsync(CancellationToken ct)
        => await central.Planes
            .AsNoTracking()
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Codigo)
            .Select(p => new ResumenPlan(
                p.Id,
                p.Codigo,
                p.Nombre,
                p.Descripcion,
                p.PrecioMensual,
                p.Moneda,
                p.Orden,
                p.Activo,
                p.CreadoEn,
                // Proyectado dentro de la consulta, no con Include: asi no viajan las
                // entidades Modulo completas para quedarse con una cadena de cada una.
                p.Modulos
                    .Where(pm => pm.Modulo!.Activo)
                    .OrderBy(pm => pm.Modulo!.Orden)
                    .Select(pm => pm.Modulo!.Clave)
                    .ToList(),
                central.Suscripciones.Count(s => s.PlanId == p.Id)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ResumenModulo>> ListarModulosAsync(CancellationToken ct)
        => await central.Modulos
            .AsNoTracking()
            .Where(m => m.Activo)
            .OrderBy(m => m.Orden)
            .ThenBy(m => m.Numero)
            .Select(m => new ResumenModulo(m.Clave, m.Numero, m.Orden))
            .ToListAsync(ct);

    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct)
        => central.Planes.AnyAsync(p => p.Codigo == codigo, ct);

    public async Task<IReadOnlyList<string>> ClavesDeModuloDesconocidasAsync(
        IReadOnlyList<string> claves, CancellationToken ct)
    {
        // Se pregunta por las que SI existen y se resta, en una sola consulta. Preguntar
        // una por una serian veintiseis viajes para armar un plan completo.
        var conocidas = await central.Modulos
            .AsNoTracking()
            .Where(m => m.Activo && claves.Contains(m.Clave))
            .Select(m => m.Clave)
            .ToListAsync(ct);

        return claves.Except(conocidas).ToArray();
    }

    public async Task<ResumenPlan> CrearAsync(AltaDePlan alta, CancellationToken ct)
    {
        var modulos = await central.Modulos
            .Where(m => m.Activo && alta.Modulos.Contains(m.Clave))
            .Select(m => new { m.Id, m.Clave, m.Orden })
            .ToListAsync(ct);

        var plan = new Plan
        {
            Codigo = alta.Codigo,
            Nombre = alta.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(alta.Descripcion) ? null : alta.Descripcion.Trim(),
            PrecioMensual = alta.PrecioMensual,
            Moneda = alta.Moneda,
            Orden = alta.Orden,
        };

        foreach (var modulo in modulos)
        {
            plan.Modulos.Add(new PlanModulo { PlanId = plan.Id, ModuloId = modulo.Id });
        }

        central.Planes.Add(plan);

        // UN SOLO SaveChanges: EF envuelve el plan y sus filas de plan_modulo en una
        // transaccion, asi que o entran todos o ninguno.
        await central.SaveChangesAsync(ct);

        return new ResumenPlan(
            plan.Id,
            plan.Codigo,
            plan.Nombre,
            plan.Descripcion,
            plan.PrecioMensual,
            plan.Moneda,
            plan.Orden,
            plan.Activo,
            // `CreadoEn` lo pone la base con `now()`, asi que la entidad recien insertada
            // lo tiene en su valor por omision. Se lee de vuelta en lugar de inventarlo:
            // un DateTime.UtcNow de aqui podria no coincidir con lo que quedo guardado.
            await central.Planes.AsNoTracking()
                .Where(p => p.Id == plan.Id)
                .Select(p => p.CreadoEn)
                .FirstAsync(ct),
            modulos.OrderBy(m => m.Orden).Select(m => m.Clave).ToArray(),
            // Recien creado no puede tener suscripciones.
            0);
    }

    public async Task<ResumenPlan?> CambiarActivoAsync(
        string codigo, bool activo, CancellationToken ct)
    {
        var filas = await central.Planes
            .Where(p => p.Codigo == codigo)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Activo, activo), ct);

        if (filas == 0)
        {
            return null;
        }

        // Se relee para devolver el plan completo —con sus modulos y sus suscripciones—
        // en lugar de armar a mano un objeto que podria discrepar de la lista.
        var planes = await ListarPlanesAsync(ct);

        return planes.FirstOrDefault(p => p.Codigo == codigo);
    }
}
