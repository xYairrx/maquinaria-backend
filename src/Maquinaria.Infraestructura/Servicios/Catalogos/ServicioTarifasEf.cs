using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Catalogos;

internal sealed class ServicioTarifasEf(ContextoEmpresa bd) : IServicioTarifas
{
    public async Task<Pagina<TarifaDto>> ListarAsync(FiltroTarifas filtro, CancellationToken ct)
    {
        var consulta = bd.Tarifas.AsNoTracking();

        // Los dos filtros van por separado y no como un enum «ambito»: una tarifa puede
        // aplicar a las dos cosas, y con un enum habria que inventar un valor «ambas».
        if (filtro.AplicaRenta is true)
        {
            consulta = consulta.Where(t => t.AplicaRenta);
        }

        if (filtro.AplicaVenta is true)
        {
            consulta = consulta.Where(t => t.AplicaVenta);
        }

        if (filtro.Unidad is UnidadTarifa unidad)
        {
            consulta = consulta.Where(t => t.Unidad == unidad);
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
            .Select(t => new TarifaDto(
                t.Id, t.Codigo, t.Nombre, t.Descripcion, t.Unidad,
                t.AplicaRenta, t.AplicaVenta, t.Activo))
            .ToListAsync(ct);

        return new Pagina<TarifaDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    public Task<TarifaDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Tarifas
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TarifaDto(
                t.Id, t.Codigo, t.Nombre, t.Descripcion, t.Unidad,
                t.AplicaRenta, t.AplicaVenta, t.Activo))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<TarifaDto>> CrearAsync(AltaTarifa alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<TarifaDto>.Invalido(invalido);
        }

        var codigo = alta.Codigo.Trim().ToUpperInvariant();

        if (await bd.Tarifas.AnyAsync(t => t.Codigo == codigo, ct))
        {
            return Resultado<TarifaDto>.Conflicto(
                $"Ya existe una tarifa con el codigo '{codigo}'.");
        }

        var tarifa = new Tarifa
        {
            Codigo = codigo,
            Nombre = alta.Nombre.Trim(),
            Descripcion = Vacio(alta.Descripcion),
            Unidad = alta.Unidad,
            AplicaRenta = alta.AplicaRenta,
            AplicaVenta = alta.AplicaVenta,
        };

        bd.Tarifas.Add(tarifa);

        return await GuardarAsync(tarifa, ct);
    }

    public async Task<Resultado<TarifaDto>> EditarAsync(
        Guid id, AltaTarifa cambio, CancellationToken ct)
    {
        if (Validar(cambio) is string invalido)
        {
            return Resultado<TarifaDto>.Invalido(invalido);
        }

        var tarifa = await bd.Tarifas.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tarifa is null)
        {
            return Resultado<TarifaDto>.NoEncontrado("La tarifa no existe.");
        }

        var codigo = cambio.Codigo.Trim().ToUpperInvariant();

        if (await bd.Tarifas.AnyAsync(t => t.Codigo == codigo && t.Id != id, ct))
        {
            return Resultado<TarifaDto>.Conflicto(
                $"Ya existe otra tarifa con el codigo '{codigo}'.");
        }

        // LA UNIDAD SE PUEDE CORREGIR pero no se congela nada al hacerlo: las lineas de
        // documentos ya emitidos guardan su propio precio e importe, asi que cambiar la
        // unidad aqui no reescribe lo que se cobro. Lo que si cambia es como se captura de
        // aqui en adelante.
        tarifa.Codigo = codigo;
        tarifa.Nombre = cambio.Nombre.Trim();
        tarifa.Descripcion = Vacio(cambio.Descripcion);
        tarifa.Unidad = cambio.Unidad;
        tarifa.AplicaRenta = cambio.AplicaRenta;
        tarifa.AplicaVenta = cambio.AplicaVenta;

        return await GuardarAsync(tarifa, ct);
    }

    public async Task<Resultado<TarifaDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var tarifa = await bd.Tarifas.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tarifa is null)
        {
            return Resultado<TarifaDto>.NoEncontrado("La tarifa no existe.");
        }

        tarifa.Activo = activo;

        return await GuardarAsync(tarifa, ct);
    }

    private async Task<Resultado<TarifaDto>> GuardarAsync(Tarifa tarifa, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<TarifaDto>.Conflicto(
                $"Ya existe una tarifa con el codigo '{tarifa.Codigo}'.");
        }

        return Resultado<TarifaDto>.Ok(new TarifaDto(
            tarifa.Id, tarifa.Codigo, tarifa.Nombre, tarifa.Descripcion, tarifa.Unidad,
            tarifa.AplicaRenta, tarifa.AplicaVenta, tarifa.Activo));
    }

    private static string? Validar(AltaTarifa alta)
        => string.IsNullOrWhiteSpace(alta.Codigo) ? "El codigo es obligatorio."
            : string.IsNullOrWhiteSpace(alta.Nombre) ? "El nombre es obligatorio."
            : !Enum.IsDefined(alta.Unidad) ? "La unidad no es valida."
            // El CHECK `tarifa_aplica_en_algo` de la base dice lo mismo; aqui se explica.
            : !alta.AplicaRenta && !alta.AplicaVenta
                ? "La tarifa tiene que aplicar a renta, a venta o a las dos."
            : null;

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
