using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Catalogos;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Catalogos;

/// <summary>
/// El catalogo de marcas.
///
/// Comparado con <see cref="ServicioCategoriasEquipoEf"/>: la FORMA es la misma —listar con
/// paginado, obtener, crear, editar, activar— y el CONTENIDO no comparte una linea. Aqui la
/// clave unica es el nombre, no un codigo; no hay descripcion; el texto busca en una sola
/// columna; y el orden solo tiene dos opciones porque no hay mas columnas por las que ordenar.
///
/// Ese es el dato que decide si conviene una base generica de catalogo: **hoy no**. Lo que se
/// repetiria en la base es la mecanica de paginar y el try/catch del UNIQUE; lo que cambia es
/// todo lo demas.
/// </summary>
internal sealed class ServicioMarcasEf(ContextoEmpresa bd) : IServicioMarcas
{
    public async Task<Pagina<MarcaDto>> ListarAsync(Filtro filtro, CancellationToken ct)
    {
        var consulta = bd.Marcas.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(m => EF.Functions.ILike(m.Nombre, $"%{texto}%"));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(m => m.Activo == activo);
        }

        var total = await consulta.LongCountAsync(ct);

        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("creado", false) => consulta.OrderBy(m => m.CreadoEn),
            ("creado", true) => consulta.OrderByDescending(m => m.CreadoEn),
            (_, true) => consulta.OrderByDescending(m => m.Nombre),
            _ => consulta.OrderBy(m => m.Nombre),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(m => new MarcaDto(m.Id, m.Nombre, m.Activo, m.Modelos.Count))
            .ToListAsync(ct);

        return new Pagina<MarcaDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    public Task<MarcaDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Marcas
            .AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new MarcaDto(m.Id, m.Nombre, m.Activo, m.Modelos.Count))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<MarcaDto>> CrearAsync(AltaMarca alta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alta.Nombre))
        {
            return Resultado<MarcaDto>.Invalido("El nombre es obligatorio.");
        }

        var nombre = alta.Nombre.Trim();

        // EL UNICO ES SOBRE EL NOMBRE TAL CUAL, sensible a mayusculas: en la base,
        // 'Caterpillar' y 'CATERPILLAR' son dos marcas distintas y el UNIQUE no lo impide.
        // Por eso la comprobacion previa usa ILIKE exacto —sin comodines— y rechaza el
        // duplicado que el motor dejaria pasar.
        if (await bd.Marcas.AnyAsync(m => EF.Functions.ILike(m.Nombre, nombre), ct))
        {
            return Resultado<MarcaDto>.Conflicto($"Ya existe la marca '{nombre}'.");
        }

        var marca = new Marca { Nombre = nombre };

        bd.Marcas.Add(marca);

        return await GuardarAsync(marca, ct);
    }

    public async Task<Resultado<MarcaDto>> EditarAsync(
        Guid id, AltaMarca cambio, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cambio.Nombre))
        {
            return Resultado<MarcaDto>.Invalido("El nombre es obligatorio.");
        }

        var marca = await bd.Marcas.FirstOrDefaultAsync(m => m.Id == id, ct);

        if (marca is null)
        {
            return Resultado<MarcaDto>.NoEncontrado("La marca no existe.");
        }

        var nombre = cambio.Nombre.Trim();

        if (await bd.Marcas.AnyAsync(m => EF.Functions.ILike(m.Nombre, nombre) && m.Id != id, ct))
        {
            return Resultado<MarcaDto>.Conflicto($"Ya existe otra marca llamada '{nombre}'.");
        }

        marca.Nombre = nombre;

        return await GuardarAsync(marca, ct);
    }

    public async Task<Resultado<MarcaDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var marca = await bd.Marcas.FirstOrDefaultAsync(m => m.Id == id, ct);

        if (marca is null)
        {
            return Resultado<MarcaDto>.NoEncontrado("La marca no existe.");
        }

        marca.Activo = activo;

        return await GuardarAsync(marca, ct);
    }

    private async Task<Resultado<MarcaDto>> GuardarAsync(Marca marca, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<MarcaDto>.Conflicto($"Ya existe la marca '{marca.Nombre}'.");
        }

        return Resultado<MarcaDto>.Ok(
            new MarcaDto(marca.Id, marca.Nombre, marca.Activo, marca.Modelos.Count));
    }
}
