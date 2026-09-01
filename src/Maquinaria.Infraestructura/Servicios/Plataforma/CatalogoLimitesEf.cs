using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Plataforma;

internal sealed class CatalogoLimitesEf(ContextoCentral central) : ICatalogoLimites
{
    public async Task<IReadOnlyList<ResumenTipoLimite>> ListarAsync(CancellationToken ct)
    {
        // `Reconocida` se rellena con `false` en la consulta y se resuelve DESPUES, con un
        // `with` en memoria. Es a proposito: `FormatoClaveLimite.EsReconocida` es una
        // llamada a metodo, y una llamada a metodo dentro de un Select es exactamente la
        // proyeccion que EF evalua en el cliente — el defecto que costo trece servicios el
        // 2026-08-28.
        //
        // Aqui no puede traducirse de ninguna forma: la lista de claves con codigo detras
        // vive en el ensamblado, no en la base. Asi que se saca del arbol de expresion en
        // lugar de dejar que EF decida.
        var filas = await central.TiposLimite
            .AsNoTracking()
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Clave)
            .Select(t => new ResumenTipoLimite(
                t.Id,
                t.Clave,
                t.Nombre,
                t.Descripcion,
                t.Unidad,
                t.ValorDefecto,
                t.Orden,
                t.Activo,
                // Relleno: lo real se pone abajo, ya fuera del arbol de expresion.
                false,
                central.TenantLimites.Count(l => l.TipoLimiteId == t.Id)))
            .ToListAsync(ct);

        return [.. filas.Select(f => f with { Reconocida = FormatoClaveLimite.EsReconocida(f.Clave) })];
    }

    public async Task<ResultadoTipoLimite> CrearAsync(AltaTipoLimite alta, CancellationToken ct)
    {
        var clave = FormatoClaveLimite.Normalizar(alta.Clave);

        if (!FormatoClaveLimite.EsValido(clave))
        {
            return ResultadoTipoLimite.Rechazado(FormatoClaveLimite.Explicacion);
        }

        var mal = Validar(alta.Nombre, alta.Unidad, alta.ValorDefecto);

        if (mal is not null)
        {
            return ResultadoTipoLimite.Rechazado(mal);
        }

        // Se comprueba antes de insertar para poder dar un motivo legible. El indice unico
        // de la base sigue siendo la garantia real —dos altas a la vez pasan las dos por
        // aqui— y ahi el fallo sale como 500, que es lo correcto para una carrera que
        // ninguna persona provoco.
        if (await central.TiposLimite.AnyAsync(t => t.Clave == clave, ct))
        {
            return ResultadoTipoLimite.Rechazado(
                $"Ya existe un tipo de limite con la clave '{clave}'. Un tipo retirado se "
                + "reactiva, no se vuelve a crear.");
        }

        var tipo = new TipoLimite
        {
            Clave = clave,
            Nombre = alta.Nombre.Trim(),
            Descripcion = alta.Descripcion?.Trim() ?? string.Empty,
            Unidad = alta.Unidad.Trim(),
            ValorDefecto = alta.ValorDefecto,
            Orden = alta.Orden,
        };

        central.TiposLimite.Add(tipo);

        await central.SaveChangesAsync(ct);

        // Recien creado no puede tener excepciones.
        return ResultadoTipoLimite.Exito(Resumir(tipo, 0));
    }

    public async Task<ResultadoTipoLimite> EditarAsync(
        string clave, CambioTipoLimite cambio, CancellationToken ct)
    {
        var mal = Validar(cambio.Nombre, cambio.Unidad, cambio.ValorDefecto);

        if (mal is not null)
        {
            return ResultadoTipoLimite.Rechazado(mal);
        }

        var tipo = await central.TiposLimite
            .FirstOrDefaultAsync(t => t.Clave == FormatoClaveLimite.Normalizar(clave), ct);

        if (tipo is null)
        {
            return ResultadoTipoLimite.Rechazado(
                $"No existe un tipo de limite con la clave '{clave}'.");
        }

        tipo.Nombre = cambio.Nombre.Trim();
        tipo.Descripcion = cambio.Descripcion?.Trim() ?? string.Empty;
        tipo.Unidad = cambio.Unidad.Trim();
        tipo.ValorDefecto = cambio.ValorDefecto;
        tipo.Orden = cambio.Orden;
        tipo.Activo = cambio.Activo;

        // Con seguimiento y SaveChanges, no ExecuteUpdateAsync, por lo mismo que en
        // LimitesTenantEf: el interceptor de auditoria solo ve lo que pasa por aqui, y
        // mover el valor por defecto cambia el cupo efectivo de todas las empresas sin
        // excepcion propia.
        await central.SaveChangesAsync(ct);

        var excepciones = await central.TenantLimites.CountAsync(l => l.TipoLimiteId == tipo.Id, ct);

        return ResultadoTipoLimite.Exito(Resumir(tipo, excepciones));
    }

    /// <returns>El motivo del rechazo, o `null` si todo esta bien.</returns>
    private static string? Validar(string nombre, string unidad, int valorDefecto)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "El nombre no puede ir vacio: es lo que se lee al comparar planes.";
        }

        if (string.IsNullOrWhiteSpace(unidad))
        {
            return "La unidad no puede ir vacia: es lo que da sentido al numero —equipos, "
                + "usuarios, GB—.";
        }

        // El mismo CHECK que la base impone, comprobado antes de llegar a ella para que el
        // panel reciba un motivo legible en lugar de un 500 con un 23514 debajo.
        if (valorDefecto < TipoLimite.Ilimitado)
        {
            return $"El valor por defecto debe ser {TipoLimite.Ilimitado} —sin limite— o un "
                + "entero mayor o igual a cero.";
        }

        return null;
    }

    private static ResumenTipoLimite Resumir(TipoLimite t, int excepciones)
        => new(
            t.Id, t.Clave, t.Nombre, t.Descripcion, t.Unidad, t.ValorDefecto, t.Orden,
            t.Activo, FormatoClaveLimite.EsReconocida(t.Clave), excepciones);
}
