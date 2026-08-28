using System.Linq.Expressions;
using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Catalogos;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Catalogos;

internal sealed class ServicioModelosEquipoEf(ContextoEmpresa bd) : IServicioModelosEquipo
{
    public async Task<Pagina<ModeloEquipoDto>> ListarAsync(
        FiltroModelosEquipo filtro, CancellationToken ct)
    {
        var consulta = bd.ModelosEquipo.AsNoTracking();

        if (filtro.MarcaId is Guid marca)
        {
            consulta = consulta.Where(m => m.MarcaId == marca);
        }

        if (filtro.TipoEquipoId is Guid tipo)
        {
            consulta = consulta.Where(m => m.TipoEquipoId == tipo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            // Busca tambien por marca: quien escribe «komatsu» espera sus modelos, no cero
            // resultados porque la marca vive en otra tabla.
            consulta = consulta.Where(m =>
                EF.Functions.ILike(m.Nombre, $"%{texto}%")
                || EF.Functions.ILike(m.Marca!.Nombre, $"%{texto}%"));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(m => m.Activo == activo);
        }

        var total = await consulta.LongCountAsync(ct);

        // El orden por defecto es marca y luego modelo: es como se lee un catalogo de
        // maquinaria y como lo espera cualquier lista desplegable.
        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("nombre", false) => consulta.OrderBy(m => m.Nombre),
            ("nombre", true) => consulta.OrderByDescending(m => m.Nombre),
            (_, true) => consulta.OrderByDescending(m => m.Marca!.Nombre)
                                 .ThenByDescending(m => m.Nombre),
            _ => consulta.OrderBy(m => m.Marca!.Nombre).ThenBy(m => m.Nombre),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(Proyeccion())
            .ToListAsync(ct);

        return new Pagina<ModeloEquipoDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    /// <summary>
    /// La proyeccion, en un solo sitio para las operaciones que la devuelven.
    ///
    /// DEVUELVE UN ARBOL DE EXPRESION, NO UN DTO. EF Core no sabe traducir una llamada a
    /// metodo, asi que con la forma anterior —`.Select(m => Proyectar(m, bd))`— evaluaba la
    /// proyeccion EN EL CLIENTE: materializaba los ModeloEquipo sin navegaciones —no hay
    /// Include— y `m.Marca!.Nombre` reventaba con NullReferenceException. Ademas,
    /// `bd.Equipos.Count(...)` corria una consulta POR FILA.
    ///
    /// Como expresion, EF traduce las dos navegaciones a JOIN y el conteo a una subconsulta
    /// correlacionada, todo en el mismo SELECT. `bd` se captura en el cierre, asi que el
    /// metodo NO puede ser static.
    /// </summary>
    private Expression<Func<ModeloEquipo, ModeloEquipoDto>> Proyeccion() => m => new ModeloEquipoDto(
        m.Id,
        m.MarcaId,
        m.Marca!.Nombre,
        m.TipoEquipoId,
        m.TipoEquipo == null ? null : m.TipoEquipo.Nombre,
        m.Nombre,
        m.Descripcion,
        m.HorasEntreServicios,
        m.Activo,
        bd.Equipos.Count(e => e.ModeloEquipoId == m.Id));

    public Task<ModeloEquipoDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.ModelosEquipo
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(Proyeccion())
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<ModeloEquipoDto>> CrearAsync(
        AltaModeloEquipo alta, CancellationToken ct)
    {
        if (await ValidarAsync(alta, ct) is string invalido)
        {
            return Resultado<ModeloEquipoDto>.Invalido(invalido);
        }

        var nombre = alta.Nombre.Trim();

        if (await ExisteAsync(alta.MarcaId, nombre, null, ct))
        {
            return Resultado<ModeloEquipoDto>.Conflicto(
                $"Esa marca ya tiene un modelo llamado '{nombre}'.");
        }

        var modelo = new ModeloEquipo
        {
            MarcaId = alta.MarcaId,
            TipoEquipoId = alta.TipoEquipoId,
            Nombre = nombre,
            Descripcion = Vacio(alta.Descripcion),
            HorasEntreServicios = alta.HorasEntreServicios,
        };

        bd.ModelosEquipo.Add(modelo);

        return await GuardarAsync(modelo, ct);
    }

    public async Task<Resultado<ModeloEquipoDto>> EditarAsync(
        Guid id, AltaModeloEquipo cambio, CancellationToken ct)
    {
        if (await ValidarAsync(cambio, ct) is string invalido)
        {
            return Resultado<ModeloEquipoDto>.Invalido(invalido);
        }

        var modelo = await bd.ModelosEquipo.FirstOrDefaultAsync(m => m.Id == id, ct);

        if (modelo is null)
        {
            return Resultado<ModeloEquipoDto>.NoEncontrado("El modelo no existe.");
        }

        var nombre = cambio.Nombre.Trim();

        if (await ExisteAsync(cambio.MarcaId, nombre, id, ct))
        {
            return Resultado<ModeloEquipoDto>.Conflicto(
                $"Esa marca ya tiene otro modelo llamado '{nombre}'.");
        }

        modelo.MarcaId = cambio.MarcaId;
        modelo.TipoEquipoId = cambio.TipoEquipoId;
        modelo.Nombre = nombre;
        modelo.Descripcion = Vacio(cambio.Descripcion);
        modelo.HorasEntreServicios = cambio.HorasEntreServicios;

        return await GuardarAsync(modelo, ct);
    }

    public async Task<Resultado<ModeloEquipoDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var modelo = await bd.ModelosEquipo.FirstOrDefaultAsync(m => m.Id == id, ct);

        if (modelo is null)
        {
            return Resultado<ModeloEquipoDto>.NoEncontrado("El modelo no existe.");
        }

        modelo.Activo = activo;

        return await GuardarAsync(modelo, ct);
    }

    /// <summary>
    /// La clave unica es (marca, nombre), asi que la comprobacion lleva las dos. Con ILIKE
    /// para que «PC200» y «pc200» de la misma marca no entren como dos modelos.
    /// </summary>
    private Task<bool> ExisteAsync(Guid marcaId, string nombre, Guid? excepto, CancellationToken ct)
        => bd.ModelosEquipo.AnyAsync(
            m => m.MarcaId == marcaId
                 && EF.Functions.ILike(m.Nombre, nombre)
                 && (excepto == null || m.Id != excepto),
            ct);

    private async Task<Resultado<ModeloEquipoDto>> GuardarAsync(
        ModeloEquipo modelo, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<ModeloEquipoDto>.Conflicto(
                $"Esa marca ya tiene un modelo llamado '{modelo.Nombre}'.");
        }

        return Resultado<ModeloEquipoDto>.Ok((await ObtenerAsync(modelo.Id, ct))!);
    }

    private async Task<string?> ValidarAsync(AltaModeloEquipo alta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alta.Nombre))
        {
            return "El nombre del modelo es obligatorio.";
        }

        // El CHECK de la base solo exige > 0; cero y negativo se rechazan aqui con mensaje.
        if (alta.HorasEntreServicios is int horas && horas <= 0)
        {
            return "Las horas entre servicios tienen que ser mayores que cero.";
        }

        if (!await bd.Marcas.AnyAsync(m => m.Id == alta.MarcaId, ct))
        {
            return "La marca no existe.";
        }

        if (alta.TipoEquipoId is Guid tipo
            && !await bd.TiposEquipo.AnyAsync(t => t.Id == tipo, ct))
        {
            return "El tipo de equipo no existe.";
        }

        return null;
    }

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
