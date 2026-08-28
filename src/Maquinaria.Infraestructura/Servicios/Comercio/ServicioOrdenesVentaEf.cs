using System.Linq.Expressions;
using Maquinaria.Aplicacion.Comercio;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Dominio.Terceros;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Comercio;

internal sealed class ServicioOrdenesVentaEf(ContextoEmpresa bd, IFolios folios)
    : IServicioOrdenesVenta
{
    private static readonly Dictionary<EstadoOrden, EstadoOrden[]> Transiciones = new()
    {
        [EstadoOrden.Borrador] = [EstadoOrden.Autorizada, EstadoOrden.Cancelada],
        [EstadoOrden.Autorizada] = [EstadoOrden.Finalizada, EstadoOrden.Cancelada],
    };

    public async Task<Pagina<OrdenVentaDto>> ListarAsync(
        FiltroOrdenes filtro, CancellationToken ct)
    {
        var consulta = bd.OrdenesVenta.AsNoTracking();

        if (filtro.ContraparteId is Guid cliente)
        {
            consulta = consulta.Where(o => o.ClienteId == cliente);
        }

        if (filtro.Estado is EstadoOrden estado)
        {
            consulta = consulta.Where(o => o.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(o =>
                EF.Functions.ILike(o.Folio, $"%{texto}%")
                || EF.Functions.ILike(o.Cliente!.RazonSocial, $"%{texto}%"));
        }

        var total = await consulta.LongCountAsync(ct);

        var filas = await consulta
            .OrderByDescending(o => o.Fecha).ThenByDescending(o => o.Folio)
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(Encabezado())
            .ToListAsync(ct);

        return new Pagina<OrdenVentaDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    /// <summary>
    /// DEVUELVE UN ARBOL DE EXPRESION, NO UN DTO.
    ///
    /// Con la forma anterior —<c>.Select(Encabezado())</c>— EF no sabia traducir la
    /// LLAMADA A METODO y corria la proyeccion EN MEMORIA. Sin <c>Include</c>, las dos
    /// navegaciones que se leen aqui —cliente y trabajador— llegaban en nulo y reventaban
    /// con <c>NullReferenceException</c> en cuanto hubiera una orden.
    ///
    /// Se busco tambien el otro defecto de esta familia —un <c>ToString()</c> de enum dentro
    /// del <c>Select</c>, que revienta incluso con la tabla vacia— y aqui no hay ninguno.
    /// </summary>
    private static Expression<Func<OrdenVenta, OrdenVentaDto>> Encabezado() => o => new OrdenVentaDto(
        o.Id, o.Folio, o.ClienteId, o.Cliente!.RazonSocial,
        o.TrabajadorId, o.Trabajador!.Nombre,
        o.Fecha, o.Estado, o.Subtotal, o.Descuento, o.Impuestos, o.Total,
        o.AutorizadaEn, o.FinalizadaEn, o.Notas,
        // `Array.Empty` y no `[]`: una EXPRESION DE COLECCION no cabe en un arbol de
        // expresion —error CS9175—. Los renglones van en una segunda consulta.
        Array.Empty<OrdenVentaDetalleDto>());

    public async Task<OrdenVentaDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var orden = await bd.OrdenesVenta
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(Encabezado())
            .FirstOrDefaultAsync(ct);

        if (orden is null)
        {
            return null;
        }

        var detalles = await bd.OrdenVentaDetalles
            .AsNoTracking()
            .Where(d => d.OrdenVentaId == id)
            .OrderBy(d => d.Orden)
            .Select(d => new OrdenVentaDetalleDto(
                d.Id, d.EquipoId, d.Equipo!.CodigoInterno, d.Equipo.Modelo!.Nombre,
                d.PrecioUnitario, d.Importe, d.Orden))
            .ToListAsync(ct);

        return orden with { Detalles = detalles };
    }

    public async Task<Resultado<OrdenVentaDto>> CrearAsync(
        AltaOrdenVenta alta, CancellationToken ct)
    {
        if (alta.Descuento < 0 || alta.Impuestos < 0)
        {
            return Resultado<OrdenVentaDto>.Invalido("Los montos no pueden ser negativos.");
        }

        var cliente = await bd.Clientes
            .Where(c => c.Id == alta.ClienteId)
            .Select(c => (EstadoCliente?)c.Estado)
            .FirstOrDefaultAsync(ct);

        if (cliente is null)
        {
            return Resultado<OrdenVentaDto>.Invalido("El cliente no existe.");
        }

        if (cliente != EstadoCliente.Activo)
        {
            return Resultado<OrdenVentaDto>.Invalido(
                $"El cliente esta {cliente} y no se le puede vender.");
        }

        if (!await bd.Trabajadores.AnyAsync(t => t.Id == alta.TrabajadorId, ct))
        {
            return Resultado<OrdenVentaDto>.Invalido("El trabajador no existe.");
        }

        var orden = new OrdenVenta
        {
            Folio = await folios.SiguienteAsync(TipoDocumento.OrdenVenta, ct),
            ClienteId = alta.ClienteId,
            TrabajadorId = alta.TrabajadorId,
            Fecha = alta.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Estado = EstadoOrden.Borrador,
            Descuento = alta.Descuento,
            Impuestos = alta.Impuestos,
            Notas = Vacio(alta.Notas),
        };

        Recalcular(orden, []);

        bd.OrdenesVenta.Add(orden);

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            bd.Entry(orden).State = EntityState.Detached;
            orden.Folio = await folios.SiguienteAsync(TipoDocumento.OrdenVenta, ct);
            bd.OrdenesVenta.Add(orden);
            await bd.SaveChangesAsync(ct);
        }

        return Resultado<OrdenVentaDto>.Ok((await ObtenerAsync(orden.Id, ct))!);
    }

    public async Task<Resultado<OrdenVentaDetalleDto>> AgregarDetalleAsync(
        Guid ordenId, AltaOrdenVentaDetalle detalle, CancellationToken ct)
    {
        var orden = await bd.OrdenesVenta.FirstOrDefaultAsync(o => o.Id == ordenId, ct);

        if (orden is null)
        {
            return Resultado<OrdenVentaDetalleDto>.NoEncontrado("La orden no existe.");
        }

        if (orden.Estado != EstadoOrden.Borrador)
        {
            return Resultado<OrdenVentaDetalleDto>.Conflicto(
                $"La orden esta {orden.Estado} y sus lineas ya no se tocan.");
        }

        if (detalle.PrecioUnitario < 0)
        {
            return Resultado<OrdenVentaDetalleDto>.Invalido(
                "El precio no puede ser negativo.");
        }

        var equipo = await bd.Equipos
            .Where(e => e.Id == detalle.EquipoId && e.EliminadoEn == null)
            .Select(e => new { e.CodigoInterno, e.Estado, e.Proposito })
            .FirstOrDefaultAsync(ct);

        if (equipo is null)
        {
            return Resultado<OrdenVentaDetalleDto>.Invalido("El equipo no existe.");
        }

        if (equipo.Estado is EstadoEquipo.Vendido or EstadoEquipo.Baja)
        {
            return Resultado<OrdenVentaDetalleDto>.Conflicto(
                $"El equipo {equipo.CodigoInterno} ya esta {equipo.Estado}.");
        }

        if (equipo.Proposito == PropositoEquipo.Renta)
        {
            // No se bloquea, se avisa con 400: marcar la maquina como vendible es una decision
            // de negocio que se toma en el expediente del equipo, no colandola por una venta.
            return Resultado<OrdenVentaDetalleDto>.Invalido(
                $"El equipo {equipo.CodigoInterno} esta marcado solo para renta. Cambia su "
                + "proposito a Venta o RentaYVenta antes de venderlo.");
        }

        var nuevo = new OrdenVentaDetalle
        {
            OrdenVentaId = ordenId,
            EquipoId = detalle.EquipoId,
            PrecioUnitario = detalle.PrecioUnitario,
            // Una maquina, una linea: el importe es el precio. No hay cantidad porque cada
            // equipo es unico.
            Importe = detalle.PrecioUnitario,
            Orden = detalle.Orden,
        };

        bd.OrdenVentaDetalles.Add(nuevo);

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<OrdenVentaDetalleDto>.Conflicto(
                $"El equipo {equipo.CodigoInterno} ya esta en esta orden.");
        }

        Recalcular(orden, await ImportesAsync(ordenId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado<OrdenVentaDetalleDto>.Ok(await DetalleAsync(nuevo.Id, ct));
    }

    public async Task<Resultado> QuitarDetalleAsync(
        Guid ordenId, Guid detalleId, CancellationToken ct)
    {
        var orden = await bd.OrdenesVenta.FirstOrDefaultAsync(o => o.Id == ordenId, ct);

        if (orden is null)
        {
            return Resultado.NoEncontrado("La orden no existe.");
        }

        if (orden.Estado != EstadoOrden.Borrador)
        {
            return Resultado.Conflicto($"La orden esta {orden.Estado}.");
        }

        var detalle = await bd.OrdenVentaDetalles
            .FirstOrDefaultAsync(d => d.Id == detalleId && d.OrdenVentaId == ordenId, ct);

        if (detalle is null)
        {
            return Resultado.NoEncontrado("La linea no existe.");
        }

        bd.OrdenVentaDetalles.Remove(detalle);

        await bd.SaveChangesAsync(ct);

        Recalcular(orden, await ImportesAsync(ordenId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado<OrdenVentaDto>> CambiarEstadoAsync(
        Guid id, EstadoOrden estado, CancellationToken ct)
    {
        if (!Enum.IsDefined(estado))
        {
            return Resultado<OrdenVentaDto>.Invalido("El estado no es valido.");
        }

        if (estado == EstadoOrden.Finalizada)
        {
            return Resultado<OrdenVentaDto>.Invalido(
                "Finalizar una venta saca los equipos del parque: usa el endpoint de "
                + "finalizacion.");
        }

        var orden = await bd.OrdenesVenta.FirstOrDefaultAsync(o => o.Id == id, ct);

        if (orden is null)
        {
            return Resultado<OrdenVentaDto>.NoEncontrado("La orden no existe.");
        }

        if (orden.Estado == estado)
        {
            return Resultado<OrdenVentaDto>.Ok((await ObtenerAsync(id, ct))!);
        }

        if (!Transiciones.TryGetValue(orden.Estado, out var permitidos)
            || !permitidos.Contains(estado))
        {
            return Resultado<OrdenVentaDto>.Conflicto(
                $"No se puede pasar de {orden.Estado} a {estado}.");
        }

        if (estado == EstadoOrden.Autorizada
            && !await bd.OrdenVentaDetalles.AnyAsync(d => d.OrdenVentaId == id, ct))
        {
            return Resultado<OrdenVentaDto>.Conflicto(
                "No se puede autorizar una orden sin equipos.");
        }

        orden.Estado = estado;

        if (estado == EstadoOrden.Autorizada)
        {
            orden.AutorizadaEn = DateTime.UtcNow;
        }

        await bd.SaveChangesAsync(ct);

        return Resultado<OrdenVentaDto>.Ok((await ObtenerAsync(id, ct))!);
    }

    public Task<DatosDeVenta?> DatosDeVentaAsync(Guid id, CancellationToken ct)
        => bd.OrdenesVenta
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new DatosDeVenta(
                o.Id, o.Folio, o.Estado,
                bd.OrdenVentaDetalles
                    .Where(d => d.OrdenVentaId == o.Id)
                    .Select(d => d.EquipoId)
                    .ToList()))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado> MarcarVendidosAsync(Guid id, CancellationToken ct)
    {
        var equipoIds = await bd.OrdenVentaDetalles
            .Where(d => d.OrdenVentaId == id)
            .Select(d => d.EquipoId)
            .ToListAsync(ct);

        var equipos = await bd.Equipos
            .Where(e => equipoIds.Contains(e.Id))
            .ToListAsync(ct);

        foreach (var equipo in equipos)
        {
            // SALE DEL PARQUE. Vendido es terminal: ningun endpoint lo saca de ahi, y la
            // consulta de disponibilidad lo excluye.
            equipo.Estado = EstadoEquipo.Vendido;
            equipo.ActualizadoEn = DateTime.UtcNow;
        }

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado<OrdenVentaDto>> MarcarFinalizadaAsync(
        Guid id, CancellationToken ct)
    {
        var orden = await bd.OrdenesVenta.FirstOrDefaultAsync(o => o.Id == id, ct);

        if (orden is null)
        {
            return Resultado<OrdenVentaDto>.NoEncontrado("La orden no existe.");
        }

        orden.Estado = EstadoOrden.Finalizada;
        orden.FinalizadaEn = DateTime.UtcNow;

        await bd.SaveChangesAsync(ct);

        return Resultado<OrdenVentaDto>.Ok((await ObtenerAsync(id, ct))!);
    }

    private Task<List<decimal>> ImportesAsync(Guid ordenId, CancellationToken ct)
        => bd.OrdenVentaDetalles
            .Where(d => d.OrdenVentaId == ordenId)
            .Select(d => d.Importe)
            .ToListAsync(ct);

    private static void Recalcular(OrdenVenta orden, List<decimal> importes)
    {
        orden.Subtotal = importes.Sum();
        orden.Total = Math.Max(0, orden.Subtotal - orden.Descuento + orden.Impuestos);
    }

    private Task<OrdenVentaDetalleDto> DetalleAsync(Guid id, CancellationToken ct)
        => bd.OrdenVentaDetalles
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new OrdenVentaDetalleDto(
                d.Id, d.EquipoId, d.Equipo!.CodigoInterno, d.Equipo.Modelo!.Nombre,
                d.PrecioUnitario, d.Importe, d.Orden))
            .FirstAsync(ct);

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
