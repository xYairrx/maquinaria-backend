using Maquinaria.Aplicacion.Comercio;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Dominio.Compras;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Comercio;

internal sealed class ServicioOrdenesCompraEf(ContextoEmpresa bd, IFolios folios)
    : IServicioOrdenesCompra
{
    private static readonly Dictionary<EstadoOrden, EstadoOrden[]> Transiciones = new()
    {
        [EstadoOrden.Borrador] = [EstadoOrden.Autorizada, EstadoOrden.Cancelada],
        [EstadoOrden.Autorizada] = [EstadoOrden.Finalizada, EstadoOrden.Cancelada],
    };

    public async Task<Pagina<OrdenCompraDto>> ListarAsync(
        FiltroOrdenes filtro, CancellationToken ct)
    {
        var consulta = bd.OrdenesCompra.AsNoTracking();

        if (filtro.ContraparteId is Guid proveedor)
        {
            consulta = consulta.Where(o => o.ProveedorId == proveedor);
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
                || EF.Functions.ILike(o.Proveedor!.RazonSocial, $"%{texto}%"));
        }

        var total = await consulta.LongCountAsync(ct);

        var filas = await consulta
            .OrderByDescending(o => o.Fecha).ThenByDescending(o => o.Folio)
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(o => Encabezado(o))
            .ToListAsync(ct);

        return new Pagina<OrdenCompraDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    private static OrdenCompraDto Encabezado(OrdenCompra o) => new(
        o.Id, o.Folio, o.ProveedorId, o.Proveedor!.RazonSocial,
        o.TrabajadorId, o.Trabajador!.Nombre,
        o.Fecha, o.Estado, o.Subtotal, o.Impuestos, o.Total,
        o.AutorizadaEn, o.FinalizadaEn, o.Notas, []);

    public async Task<OrdenCompraDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var orden = await bd.OrdenesCompra
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => Encabezado(o))
            .FirstOrDefaultAsync(ct);

        if (orden is null)
        {
            return null;
        }

        var detalles = await bd.OrdenCompraDetalles
            .AsNoTracking()
            .Where(d => d.OrdenCompraId == id)
            .OrderBy(d => d.Orden)
            .Select(d => new OrdenCompraDetalleDto(
                d.Id, d.ModeloEquipoId, d.ModeloEquipo!.Marca!.Nombre, d.ModeloEquipo.Nombre,
                d.EquipoId, d.Equipo == null ? null : d.Equipo.CodigoInterno,
                d.NumeroSerie, d.Anio, d.Cantidad, d.CostoUnitario, d.Importe, d.Orden))
            .ToListAsync(ct);

        return orden with { Detalles = detalles };
    }

    public async Task<Resultado<OrdenCompraDto>> CrearAsync(
        AltaOrdenCompra alta, CancellationToken ct)
    {
        if (alta.Impuestos < 0)
        {
            return Resultado<OrdenCompraDto>.Invalido("Los impuestos no pueden ser negativos.");
        }

        if (!await bd.Proveedores.AnyAsync(p => p.Id == alta.ProveedorId && p.Activo, ct))
        {
            return Resultado<OrdenCompraDto>.Invalido(
                "El proveedor no existe o esta retirado.");
        }

        if (!await bd.Trabajadores.AnyAsync(t => t.Id == alta.TrabajadorId, ct))
        {
            return Resultado<OrdenCompraDto>.Invalido("El trabajador no existe.");
        }

        var orden = new OrdenCompra
        {
            Folio = await folios.SiguienteAsync(TipoDocumento.OrdenCompra, ct),
            ProveedorId = alta.ProveedorId,
            TrabajadorId = alta.TrabajadorId,
            Fecha = alta.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Estado = EstadoOrden.Borrador,
            Impuestos = alta.Impuestos,
            Notas = Vacio(alta.Notas),
        };

        Recalcular(orden, []);

        bd.OrdenesCompra.Add(orden);

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            bd.Entry(orden).State = EntityState.Detached;
            orden.Folio = await folios.SiguienteAsync(TipoDocumento.OrdenCompra, ct);
            bd.OrdenesCompra.Add(orden);
            await bd.SaveChangesAsync(ct);
        }

        return Resultado<OrdenCompraDto>.Ok((await ObtenerAsync(orden.Id, ct))!);
    }

    public async Task<Resultado<OrdenCompraDetalleDto>> AgregarDetalleAsync(
        Guid ordenId, AltaOrdenCompraDetalle detalle, CancellationToken ct)
    {
        var orden = await bd.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == ordenId, ct);

        if (orden is null)
        {
            return Resultado<OrdenCompraDetalleDto>.NoEncontrado("La orden no existe.");
        }

        if (orden.Estado != EstadoOrden.Borrador)
        {
            return Resultado<OrdenCompraDetalleDto>.Conflicto(
                $"La orden esta {orden.Estado} y sus lineas ya no se tocan.");
        }

        if (detalle.Cantidad <= 0 || detalle.CostoUnitario < 0)
        {
            return Resultado<OrdenCompraDetalleDto>.Invalido(
                "Cantidad mayor que cero y costo no negativo.");
        }

        // UNA LINEA, UNA MAQUINA. Ver la nota de AltaOrdenCompraDetalle: el detalle tiene un
        // solo equipo_id con indice unico, asi que no puede producir tres equipos. Y ademas es
        // lo correcto: cada maquina tiene su numero de serie.
        if (detalle.Cantidad != 1)
        {
            return Resultado<OrdenCompraDetalleDto>.Invalido(
                "Cada linea registra una maquina: captura una linea por equipo, con su numero "
                + "de serie.");
        }

        if (detalle.Anio is int anio && (anio < 1900 || anio > 2200))
        {
            return Resultado<OrdenCompraDetalleDto>.Invalido(
                "El anio tiene que estar entre 1900 y 2200.");
        }

        if (!await bd.ModelosEquipo.AnyAsync(m => m.Id == detalle.ModeloEquipoId, ct))
        {
            return Resultado<OrdenCompraDetalleDto>.Invalido("El modelo no existe.");
        }

        var nuevo = new OrdenCompraDetalle
        {
            OrdenCompraId = ordenId,
            ModeloEquipoId = detalle.ModeloEquipoId,
            NumeroSerie = Vacio(detalle.NumeroSerie),
            Anio = detalle.Anio,
            Cantidad = detalle.Cantidad,
            CostoUnitario = detalle.CostoUnitario,
            Importe = detalle.Cantidad * detalle.CostoUnitario,
            Orden = detalle.Orden,
        };

        bd.OrdenCompraDetalles.Add(nuevo);

        await bd.SaveChangesAsync(ct);

        Recalcular(orden, await ImportesAsync(ordenId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado<OrdenCompraDetalleDto>.Ok(await DetalleAsync(nuevo.Id, ct));
    }

    public async Task<Resultado> QuitarDetalleAsync(
        Guid ordenId, Guid detalleId, CancellationToken ct)
    {
        var orden = await bd.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == ordenId, ct);

        if (orden is null)
        {
            return Resultado.NoEncontrado("La orden no existe.");
        }

        if (orden.Estado != EstadoOrden.Borrador)
        {
            return Resultado.Conflicto($"La orden esta {orden.Estado}.");
        }

        var detalle = await bd.OrdenCompraDetalles
            .FirstOrDefaultAsync(d => d.Id == detalleId && d.OrdenCompraId == ordenId, ct);

        if (detalle is null)
        {
            return Resultado.NoEncontrado("La linea no existe.");
        }

        bd.OrdenCompraDetalles.Remove(detalle);

        await bd.SaveChangesAsync(ct);

        Recalcular(orden, await ImportesAsync(ordenId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado<OrdenCompraDto>> CambiarEstadoAsync(
        Guid id, EstadoOrden estado, CancellationToken ct)
    {
        if (!Enum.IsDefined(estado))
        {
            return Resultado<OrdenCompraDto>.Invalido("El estado no es valido.");
        }

        if (estado == EstadoOrden.Finalizada)
        {
            // Finalizar registra equipos en el catalogo: es el Proceso, no un cambio de estado.
            return Resultado<OrdenCompraDto>.Invalido(
                "Finalizar una orden registra los equipos: usa el endpoint de finalizacion.");
        }

        var orden = await bd.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == id, ct);

        if (orden is null)
        {
            return Resultado<OrdenCompraDto>.NoEncontrado("La orden no existe.");
        }

        if (orden.Estado == estado)
        {
            return Resultado<OrdenCompraDto>.Ok((await ObtenerAsync(id, ct))!);
        }

        if (!Transiciones.TryGetValue(orden.Estado, out var permitidos)
            || !permitidos.Contains(estado))
        {
            return Resultado<OrdenCompraDto>.Conflicto(
                $"No se puede pasar de {orden.Estado} a {estado}.");
        }

        if (estado == EstadoOrden.Autorizada
            && !await bd.OrdenCompraDetalles.AnyAsync(d => d.OrdenCompraId == id, ct))
        {
            return Resultado<OrdenCompraDto>.Conflicto(
                "No se puede autorizar una orden sin lineas.");
        }

        orden.Estado = estado;

        if (estado == EstadoOrden.Autorizada)
        {
            orden.AutorizadaEn = DateTime.UtcNow;
        }

        await bd.SaveChangesAsync(ct);

        return Resultado<OrdenCompraDto>.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<Resultado<Guid>> RegistrarEquipoAsync(
        Guid ordenId, RegistroDeEquipo registro, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(registro.CodigoInterno))
        {
            return Resultado<Guid>.Invalido("El codigo interno del equipo es obligatorio.");
        }

        var detalle = await bd.OrdenCompraDetalles
            .FirstOrDefaultAsync(
                d => d.Id == registro.DetalleId && d.OrdenCompraId == ordenId, ct);

        if (detalle is null)
        {
            return Resultado<Guid>.NoEncontrado("La linea no existe en esta orden.");
        }

        if (detalle.EquipoId is not null)
        {
            return Resultado<Guid>.Conflicto(
                "Esa linea ya registro su equipo.");
        }

        var codigo = registro.CodigoInterno.Trim().ToUpperInvariant();

        if (await bd.Equipos.AnyAsync(e => e.CodigoInterno == codigo, ct))
        {
            return Resultado<Guid>.Conflicto(
                $"Ya existe un equipo con el codigo '{codigo}'.");
        }

        if (!await bd.TiposEquipo.AnyAsync(t => t.Id == registro.TipoEquipoId, ct))
        {
            return Resultado<Guid>.Invalido("El tipo de equipo no existe.");
        }

        var equipo = new Equipo
        {
            CodigoInterno = codigo,
            ModeloEquipoId = detalle.ModeloEquipoId,
            TipoEquipoId = registro.TipoEquipoId,
            UbicacionId = registro.UbicacionId,
            NumeroSerie = detalle.NumeroSerie,
            Anio = detalle.Anio,
            // Nace Disponible y con proposito de renta: es maquinaria que entra al parque.
            Estado = EstadoEquipo.Disponible,
            Proposito = PropositoEquipo.Renta,
            // ORIGEN COMPRA, que es lo que hace rastreable de donde salio: desde el equipo se
            // llega al proveedor por orden_compra_detalle → orden_compra → proveedor. Por eso
            // `equipo` no necesita proveedor_id.
            Origen = OrigenEquipo.Compra,
            FechaAdquisicion = DateOnly.FromDateTime(DateTime.UtcNow),
            CostoAdquisicion = detalle.CostoUnitario,
            ValorActual = detalle.CostoUnitario,
        };

        bd.Equipos.Add(equipo);

        detalle.EquipoId = equipo.Id;

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<Guid>.Conflicto(
                $"Ya existe un equipo con el codigo '{codigo}'.");
        }
        catch (DbUpdateException excepcion)
            when (excepcion.Estado() == ErroresPostgres.Excepcion)
        {
            // El trigger equipo_exigir_almacen: la ubicacion no almacena.
            return Resultado<Guid>.Invalido(
                "La ubicacion tiene que ser una bodega o un patio: una sucursal no guarda "
                + "maquinas.");
        }

        return Resultado<Guid>.Ok(equipo.Id);
    }

    public async Task<Resultado<OrdenCompraDto>> MarcarFinalizadaAsync(
        Guid id, CancellationToken ct)
    {
        var orden = await bd.OrdenesCompra.FirstOrDefaultAsync(o => o.Id == id, ct);

        if (orden is null)
        {
            return Resultado<OrdenCompraDto>.NoEncontrado("La orden no existe.");
        }

        // El CHECK orden_compra_finalizacion exige que los dos vayan juntos.
        orden.Estado = EstadoOrden.Finalizada;
        orden.FinalizadaEn = DateTime.UtcNow;

        await bd.SaveChangesAsync(ct);

        return Resultado<OrdenCompraDto>.Ok((await ObtenerAsync(id, ct))!);
    }

    private Task<List<decimal>> ImportesAsync(Guid ordenId, CancellationToken ct)
        => bd.OrdenCompraDetalles
            .Where(d => d.OrdenCompraId == ordenId)
            .Select(d => d.Importe)
            .ToListAsync(ct);

    private static void Recalcular(OrdenCompra orden, List<decimal> importes)
    {
        orden.Subtotal = importes.Sum();
        orden.Total = orden.Subtotal + orden.Impuestos;
    }

    private Task<OrdenCompraDetalleDto> DetalleAsync(Guid id, CancellationToken ct)
        => bd.OrdenCompraDetalles
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new OrdenCompraDetalleDto(
                d.Id, d.ModeloEquipoId, d.ModeloEquipo!.Marca!.Nombre, d.ModeloEquipo.Nombre,
                d.EquipoId, d.Equipo == null ? null : d.Equipo.CodigoInterno,
                d.NumeroSerie, d.Anio, d.Cantidad, d.CostoUnitario, d.Importe, d.Orden))
            .FirstAsync(ct);

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
