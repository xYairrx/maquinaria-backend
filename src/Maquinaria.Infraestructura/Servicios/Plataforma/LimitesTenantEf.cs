using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Plataforma;

internal sealed class LimitesTenantEf(ContextoCentral central) : ILimitesTenant
{
    public async Task<IReadOnlyList<LimiteDeEmpresa>?> ListarAsync(
        string slug, CancellationToken ct)
    {
        var tenantId = await IdDeAsync(slug, ct);

        return tenantId is null ? null : await ResolverAsync(tenantId.Value, ct);
    }

    public async Task<ResultadoLimites> FijarAsync(
        string slug, string clave, int valor, CancellationToken ct)
    {
        // El mismo CHECK que la base impone, comprobado antes de llegar a ella: asi el
        // panel recibe un motivo legible en lugar de un 500 con un 23514 debajo.
        if (valor < TipoLimite.Ilimitado)
        {
            return ResultadoLimites.Rechazado(
                $"El valor debe ser {TipoLimite.Ilimitado} —sin limite— o un entero mayor "
                + "o igual a cero.");
        }

        var tenantId = await IdDeAsync(slug, ct);

        if (tenantId is null)
        {
            return ResultadoLimites.SinEmpresa();
        }

        var tipo = await central.TiposLimite
            .Where(t => t.Clave == clave && t.Activo)
            .Select(t => new { t.Id, t.Nombre })
            .FirstOrDefaultAsync(ct);

        if (tipo is null)
        {
            // Se dicen las claves validas: con "tipo de limite desconocido" a secas,
            // quien captura tiene que adivinar. Mismo criterio que las claves de modulo.
            return ResultadoLimites.Rechazado(
                $"No existe un tipo de limite activo con la clave '{clave}'. "
                + $"Los validos son: {string.Join(", ", ClavesLimite.Todas)}.");
        }

        var existente = await central.TenantLimites
            .FirstOrDefaultAsync(
                l => l.TenantId == tenantId.Value && l.TipoLimiteId == tipo.Id, ct);

        if (existente is null)
        {
            central.TenantLimites.Add(new TenantLimite
            {
                TenantId = tenantId.Value,
                TipoLimiteId = tipo.Id,
                Valor = valor,
            });
        }
        else
        {
            existente.Valor = valor;
        }

        // CON SEGUIMIENTO Y SaveChanges, no ExecuteUpdateAsync, y es deliberado: mover el
        // cupo de un cliente es una de las decisiones mas privilegiadas del sistema y el
        // interceptor de auditoria solo ve lo que pasa por SaveChanges. Con la version
        // rapida el cambio no quedaria en ninguna bitacora.
        await central.SaveChangesAsync(ct);

        return ResultadoLimites.Exito(await ResolverAsync(tenantId.Value, ct));
    }

    public async Task<ResultadoLimites> QuitarAsync(
        string slug, string clave, CancellationToken ct)
    {
        var tenantId = await IdDeAsync(slug, ct);

        if (tenantId is null)
        {
            return ResultadoLimites.SinEmpresa();
        }

        // Sin exigir Activo: un tipo retirado del catalogo puede tener excepciones vivas
        // de antes, y no poder quitarlas las dejaria congeladas para siempre.
        var fila = await central.TenantLimites
            .FirstOrDefaultAsync(
                l => l.TenantId == tenantId.Value && l.TipoLimite!.Clave == clave, ct);

        if (fila is not null)
        {
            central.TenantLimites.Remove(fila);

            await central.SaveChangesAsync(ct);
        }

        // Sin fila que quitar tampoco es un error: el estado que se pedia —"esta empresa
        // usa el valor por defecto"— es el que ya habia.
        return ResultadoLimites.Exito(await ResolverAsync(tenantId.Value, ct));
    }

    private Task<Guid?> IdDeAsync(string slug, CancellationToken ct)
        => central.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == slug)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// La cadena de resolucion de dos niveles, hecha en la base y en una sola consulta:
    /// se recorre el CATALOGO —no las excepciones— y a cada tipo se le busca la suya.
    ///
    /// Al reves —recorrer tenant_limite— saldrian solo los cupos negociados, que es
    /// justo la mitad que la pantalla no puede dejar de mostrar.
    /// </summary>
    private async Task<IReadOnlyList<LimiteDeEmpresa>> ResolverAsync(
        Guid tenantId, CancellationToken ct)
        => await central.TiposLimite
            .AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Clave)
            .Select(t => new LimiteDeEmpresa(
                t.Clave,
                t.Nombre,
                t.Descripcion,
                t.Unidad,
                // La subconsulta se escribe DENTRO del Select y no en un metodo aparte:
                // una proyeccion por llamada a metodo la evalua EF en el cliente, que es
                // el defecto que costo trece servicios el 2026-08-28.
                central.TenantLimites
                    .Where(l => l.TenantId == tenantId && l.TipoLimiteId == t.Id)
                    .Select(l => (int?)l.Valor)
                    .FirstOrDefault() ?? t.ValorDefecto,
                t.ValorDefecto,
                central.TenantLimites
                    .Any(l => l.TenantId == tenantId && l.TipoLimiteId == t.Id),
                t.Orden))
            .ToListAsync(ct);
}
