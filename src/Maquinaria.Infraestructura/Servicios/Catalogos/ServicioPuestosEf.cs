using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Organizacion;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Catalogos;

internal sealed class ServicioPuestosEf(ContextoEmpresa bd) : IServicioPuestos
{
    public async Task<Pagina<PuestoDto>> ListarAsync(Filtro filtro, CancellationToken ct)
    {
        var consulta = bd.Puestos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(p =>
                EF.Functions.ILike(p.Nombre, $"%{texto}%")
                || EF.Functions.ILike(p.Codigo, $"%{texto}%"));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(p => p.Activo == activo);
        }

        var total = await consulta.LongCountAsync(ct);

        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("codigo", false) => consulta.OrderBy(p => p.Codigo),
            ("codigo", true) => consulta.OrderByDescending(p => p.Codigo),
            (_, true) => consulta.OrderByDescending(p => p.Nombre),
            _ => consulta.OrderBy(p => p.Nombre),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(p => new PuestoDto(
                p.Id, p.Codigo, p.Nombre, p.Descripcion, p.Activo, p.Trabajadores.Count))
            .ToListAsync(ct);

        return new Pagina<PuestoDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    public Task<PuestoDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Puestos
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new PuestoDto(
                p.Id, p.Codigo, p.Nombre, p.Descripcion, p.Activo, p.Trabajadores.Count))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<PuestoDto>> CrearAsync(AltaPuesto alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<PuestoDto>.Invalido(invalido);
        }

        var codigo = alta.Codigo.Trim().ToUpperInvariant();

        if (await bd.Puestos.AnyAsync(p => p.Codigo == codigo, ct))
        {
            return Resultado<PuestoDto>.Conflicto($"Ya existe un puesto con el codigo '{codigo}'.");
        }

        var puesto = new Puesto
        {
            Codigo = codigo,
            Nombre = alta.Nombre.Trim(),
            Descripcion = Vacio(alta.Descripcion),
        };

        bd.Puestos.Add(puesto);

        return await GuardarAsync(puesto, ct);
    }

    public async Task<Resultado<PuestoDto>> EditarAsync(
        Guid id, AltaPuesto cambio, CancellationToken ct)
    {
        if (Validar(cambio) is string invalido)
        {
            return Resultado<PuestoDto>.Invalido(invalido);
        }

        var puesto = await bd.Puestos.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (puesto is null)
        {
            return Resultado<PuestoDto>.NoEncontrado("El puesto no existe.");
        }

        var codigo = cambio.Codigo.Trim().ToUpperInvariant();

        if (await bd.Puestos.AnyAsync(p => p.Codigo == codigo && p.Id != id, ct))
        {
            return Resultado<PuestoDto>.Conflicto(
                $"Ya existe otro puesto con el codigo '{codigo}'.");
        }

        puesto.Codigo = codigo;
        puesto.Nombre = cambio.Nombre.Trim();
        puesto.Descripcion = Vacio(cambio.Descripcion);

        return await GuardarAsync(puesto, ct);
    }

    public async Task<Resultado<PuestoDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var puesto = await bd.Puestos.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (puesto is null)
        {
            return Resultado<PuestoDto>.NoEncontrado("El puesto no existe.");
        }

        puesto.Activo = activo;

        return await GuardarAsync(puesto, ct);
    }

    private async Task<Resultado<PuestoDto>> GuardarAsync(Puesto puesto, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<PuestoDto>.Conflicto(
                $"Ya existe un puesto con el codigo '{puesto.Codigo}'.");
        }

        return Resultado<PuestoDto>.Ok(new PuestoDto(
            puesto.Id, puesto.Codigo, puesto.Nombre, puesto.Descripcion,
            puesto.Activo, puesto.Trabajadores.Count));
    }

    private static string? Validar(AltaPuesto alta)
        => string.IsNullOrWhiteSpace(alta.Codigo) ? "El codigo es obligatorio."
            : string.IsNullOrWhiteSpace(alta.Nombre) ? "El nombre es obligatorio."
            : alta.Codigo.Trim().Length > 30 ? "El codigo no puede pasar de 30 caracteres."
            : null;

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
