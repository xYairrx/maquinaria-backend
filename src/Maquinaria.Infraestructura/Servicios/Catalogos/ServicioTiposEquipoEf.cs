using System.Linq.Expressions;
using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Catalogos;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Catalogos;

internal sealed class ServicioTiposEquipoEf(ContextoEmpresa bd) : IServicioTiposEquipo
{
    public async Task<Pagina<TipoEquipoDto>> ListarAsync(
        FiltroTiposEquipo filtro, CancellationToken ct)
    {
        var consulta = bd.TiposEquipo.AsNoTracking();

        if (filtro.CategoriaEquipoId is Guid categoria)
        {
            consulta = consulta.Where(t => t.CategoriaEquipoId == categoria);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(t =>
                EF.Functions.ILike(t.Nombre, $"%{texto}%")
                || EF.Functions.ILike(t.Codigo, $"%{texto}%"));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(t => t.Activo == activo);
        }

        var total = await consulta.LongCountAsync(ct);

        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("codigo", false) => consulta.OrderBy(t => t.Codigo),
            ("codigo", true) => consulta.OrderByDescending(t => t.Codigo),
            (_, true) => consulta.OrderByDescending(t => t.Nombre),
            _ => consulta.OrderBy(t => t.Nombre),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(Proyeccion())
            .ToListAsync(ct);

        return new Pagina<TipoEquipoDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    /// <summary>
    /// La proyeccion, en un solo sitio para las cuatro operaciones que la devuelven.
    ///
    /// DEVUELVE UN ARBOL DE EXPRESION, NO UN DTO, y ese detalle es la diferencia entre que
    /// EF traduzca esto a SQL o lo ejecute en memoria.
    ///
    /// Antes era `private static TipoEquipoDto Proyectar(TipoEquipo t, ContextoEmpresa bd)` y
    /// se usaba como `.Select(t => Proyectar(t, bd))`. EF Core NO SABE TRADUCIR UNA LLAMADA A
    /// METODO, asi que evaluaba la proyeccion en el cliente: materializaba los TipoEquipo
    /// —sin navegaciones, porque no hay Include— y ahi `t.Categoria!.Nombre` reventaba con
    /// NullReferenceException. El `!` era una promesa al compilador que el runtime no cumplia.
    ///
    /// Y habia un segundo dano invisible: `bd.Equipos.Count(...)` tambien corria en cliente,
    /// o sea UNA CONSULTA POR FILA. Justo lo contrario de lo que decia este comentario.
    ///
    /// Como expresion, EF traduce la navegacion a un JOIN y el conteo a una subconsulta
    /// correlacionada, las dos en el mismo SELECT. `bd` se captura en el cierre, asi que el
    /// metodo NO puede ser static.
    ///
    /// El conteo de equipos es una SUBCONSULTA CORRELACIONADA y no una navegacion, porque
    /// <c>TipoEquipo</c> no expone la coleccion: el modelo la dejo fuera a proposito —un tipo
    /// puede tener miles de equipos y nadie quiere esa propiedad cargable—.
    /// </summary>
    private Expression<Func<TipoEquipo, TipoEquipoDto>> Proyeccion() => t => new TipoEquipoDto(
        t.Id,
        t.Codigo,
        t.Nombre,
        t.CategoriaEquipoId,
        t.Categoria!.Nombre,
        t.Activo,
        bd.Equipos.Count(e => e.TipoEquipoId == t.Id));

    public Task<TipoEquipoDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.TiposEquipo
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(Proyeccion())
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<TipoEquipoDto>> CrearAsync(
        AltaTipoEquipo alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<TipoEquipoDto>.Invalido(invalido);
        }

        // La FK tambien lo impediria, pero como excepcion de Npgsql y sin decir cual de las
        // dos llaves falla. Comprobarlo aqui da el mensaje util.
        if (!await bd.CategoriasEquipo.AnyAsync(c => c.Id == alta.CategoriaEquipoId, ct))
        {
            return Resultado<TipoEquipoDto>.Invalido("La categoria no existe.");
        }

        var codigo = alta.Codigo.Trim().ToUpperInvariant();

        if (await bd.TiposEquipo.AnyAsync(t => t.Codigo == codigo, ct))
        {
            return Resultado<TipoEquipoDto>.Conflicto(
                $"Ya existe un tipo con el codigo '{codigo}'.");
        }

        var tipo = new TipoEquipo
        {
            CategoriaEquipoId = alta.CategoriaEquipoId,
            Codigo = codigo,
            Nombre = alta.Nombre.Trim(),
        };

        bd.TiposEquipo.Add(tipo);

        return await GuardarAsync(tipo, ct);
    }

    public async Task<Resultado<TipoEquipoDto>> EditarAsync(
        Guid id, AltaTipoEquipo cambio, CancellationToken ct)
    {
        if (Validar(cambio) is string invalido)
        {
            return Resultado<TipoEquipoDto>.Invalido(invalido);
        }

        var tipo = await bd.TiposEquipo.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tipo is null)
        {
            return Resultado<TipoEquipoDto>.NoEncontrado("El tipo de equipo no existe.");
        }

        if (!await bd.CategoriasEquipo.AnyAsync(c => c.Id == cambio.CategoriaEquipoId, ct))
        {
            return Resultado<TipoEquipoDto>.Invalido("La categoria no existe.");
        }

        var codigo = cambio.Codigo.Trim().ToUpperInvariant();

        if (await bd.TiposEquipo.AnyAsync(t => t.Codigo == codigo && t.Id != id, ct))
        {
            return Resultado<TipoEquipoDto>.Conflicto(
                $"Ya existe otro tipo con el codigo '{codigo}'.");
        }

        tipo.CategoriaEquipoId = cambio.CategoriaEquipoId;
        tipo.Codigo = codigo;
        tipo.Nombre = cambio.Nombre.Trim();

        return await GuardarAsync(tipo, ct);
    }

    public async Task<Resultado<TipoEquipoDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var tipo = await bd.TiposEquipo.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tipo is null)
        {
            return Resultado<TipoEquipoDto>.NoEncontrado("El tipo de equipo no existe.");
        }

        tipo.Activo = activo;

        return await GuardarAsync(tipo, ct);
    }

    private async Task<Resultado<TipoEquipoDto>> GuardarAsync(
        TipoEquipo tipo, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<TipoEquipoDto>.Conflicto(
                $"Ya existe un tipo con el codigo '{tipo.Codigo}'.");
        }

        // Se relee para traer el nombre de la categoria y el conteo sin adivinarlos.
        return Resultado<TipoEquipoDto>.Ok((await ObtenerAsync(tipo.Id, ct))!);
    }

    private static string? Validar(AltaTipoEquipo alta)
        => alta.CategoriaEquipoId == Guid.Empty ? "La categoria es obligatoria."
            : string.IsNullOrWhiteSpace(alta.Codigo) ? "El codigo es obligatorio."
            : string.IsNullOrWhiteSpace(alta.Nombre) ? "El nombre es obligatorio."
            : alta.Codigo.Trim().Length > 30 ? "El codigo no puede pasar de 30 caracteres."
            : null;
}
