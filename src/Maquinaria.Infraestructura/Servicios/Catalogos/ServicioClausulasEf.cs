using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Catalogos;

internal sealed class ServicioClausulasEf(ContextoEmpresa bd) : IServicioClausulas
{
    public async Task<Pagina<ClausulaDto>> ListarAsync(
        FiltroClausulas filtro, CancellationToken ct)
    {
        var consulta = bd.Clausulas.AsNoTracking();

        if (filtro.Obligatoria is bool obligatoria)
        {
            consulta = consulta.Where(c => c.Obligatoria == obligatoria);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            // Busca tambien DENTRO del texto de la clausula: es un catalogo de parrafos, y
            // quien busca «penalizacion» quiere la que la menciona, no solo la que la titula.
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.Titulo, $"%{texto}%")
                || EF.Functions.ILike(c.Codigo, $"%{texto}%")
                || EF.Functions.ILike(c.Texto, $"%{texto}%"));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(c => c.Activo == activo);
        }

        var total = await consulta.LongCountAsync(ct);

        // Por defecto por ORDEN y no por titulo: es el orden en que se imprimen en el
        // contrato, y es lo que el usuario esta administrando cuando abre esta pantalla.
        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("titulo", false) => consulta.OrderBy(c => c.Titulo),
            ("titulo", true) => consulta.OrderByDescending(c => c.Titulo),
            (_, true) => consulta.OrderByDescending(c => c.Orden),
            _ => consulta.OrderBy(c => c.Orden).ThenBy(c => c.Titulo),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(c => new ClausulaDto(
                c.Id, c.Codigo, c.Titulo, c.Texto, c.Orden, c.Obligatoria, c.Activo))
            .ToListAsync(ct);

        return new Pagina<ClausulaDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    public Task<ClausulaDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Clausulas
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ClausulaDto(
                c.Id, c.Codigo, c.Titulo, c.Texto, c.Orden, c.Obligatoria, c.Activo))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<ClausulaDto>> CrearAsync(AltaClausula alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<ClausulaDto>.Invalido(invalido);
        }

        var codigo = alta.Codigo.Trim().ToUpperInvariant();

        if (await bd.Clausulas.AnyAsync(c => c.Codigo == codigo, ct))
        {
            return Resultado<ClausulaDto>.Conflicto(
                $"Ya existe una clausula con el codigo '{codigo}'.");
        }

        var clausula = new Clausula
        {
            Codigo = codigo,
            Titulo = alta.Titulo.Trim(),
            Texto = alta.Texto.Trim(),
            Orden = alta.Orden,
            Obligatoria = alta.Obligatoria,
        };

        bd.Clausulas.Add(clausula);

        return await GuardarAsync(clausula, ct);
    }

    public async Task<Resultado<ClausulaDto>> EditarAsync(
        Guid id, AltaClausula cambio, CancellationToken ct)
    {
        if (Validar(cambio) is string invalido)
        {
            return Resultado<ClausulaDto>.Invalido(invalido);
        }

        var clausula = await bd.Clausulas.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (clausula is null)
        {
            return Resultado<ClausulaDto>.NoEncontrado("La clausula no existe.");
        }

        var codigo = cambio.Codigo.Trim().ToUpperInvariant();

        if (await bd.Clausulas.AnyAsync(c => c.Codigo == codigo && c.Id != id, ct))
        {
            return Resultado<ClausulaDto>.Conflicto(
                $"Ya existe otra clausula con el codigo '{codigo}'.");
        }

        // SE PUEDE CORREGIR EL TEXTO sin miedo: los contratos ya generados guardan su propia
        // copia. Ver la nota de IServicioClausulas.
        clausula.Codigo = codigo;
        clausula.Titulo = cambio.Titulo.Trim();
        clausula.Texto = cambio.Texto.Trim();
        clausula.Orden = cambio.Orden;
        clausula.Obligatoria = cambio.Obligatoria;
        clausula.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(clausula, ct);
    }

    public async Task<Resultado<ClausulaDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var clausula = await bd.Clausulas.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (clausula is null)
        {
            return Resultado<ClausulaDto>.NoEncontrado("La clausula no existe.");
        }

        clausula.Activo = activo;
        clausula.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(clausula, ct);
    }

    private async Task<Resultado<ClausulaDto>> GuardarAsync(
        Clausula clausula, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<ClausulaDto>.Conflicto(
                $"Ya existe una clausula con el codigo '{clausula.Codigo}'.");
        }

        return Resultado<ClausulaDto>.Ok(new ClausulaDto(
            clausula.Id, clausula.Codigo, clausula.Titulo, clausula.Texto,
            clausula.Orden, clausula.Obligatoria, clausula.Activo));
    }

    private static string? Validar(AltaClausula alta)
        => string.IsNullOrWhiteSpace(alta.Codigo) ? "El codigo es obligatorio."
            : string.IsNullOrWhiteSpace(alta.Titulo) ? "El titulo es obligatorio."
            // El CHECK `clausula_texto_no_vacio` lo exige en la base; aqui con mensaje.
            : string.IsNullOrWhiteSpace(alta.Texto) ? "El texto de la clausula es obligatorio."
            : alta.Orden < 0 ? "El orden no puede ser negativo."
            : null;
}
