using System.Linq.Expressions;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Rentas;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Dominio.Terceros;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Rentas;

internal sealed class ServicioRentasEf(ContextoEmpresa bd, IFolios folios) : IServicioRentas
{
    /// <summary>
    /// Las transiciones de la fase. Confirmar, cerrar y cancelar las hacen Procesos porque
    /// mueven el calendario; las otras dos son solo estado.
    /// </summary>
    private static readonly Dictionary<EstadoRenta, EstadoRenta[]> Transiciones = new()
    {
        [EstadoRenta.Borrador] = [EstadoRenta.Confirmada, EstadoRenta.Cancelada],
        [EstadoRenta.Confirmada] = [EstadoRenta.Activa, EstadoRenta.Cancelada],
        [EstadoRenta.Activa] = [EstadoRenta.Devuelta, EstadoRenta.Cerrada],
        [EstadoRenta.Devuelta] = [EstadoRenta.Cerrada],
    };

    public async Task<Pagina<RentaDto>> ListarAsync(FiltroRentas filtro, CancellationToken ct)
    {
        var consulta = bd.Rentas.AsNoTracking();

        if (filtro.ClienteId is Guid cliente)
        {
            consulta = consulta.Where(r => r.ClienteId == cliente);
        }

        if (filtro.EquipoId is Guid equipo)
        {
            // Por equipo se filtra a traves de las lineas: es «que rentas ha tenido esta
            // maquina», la pregunta del expediente.
            consulta = consulta.Where(r => bd.RentaLineas.Any(
                l => l.RentaId == r.Id && l.EquipoId == equipo));
        }

        if (filtro.Estado is EstadoRenta estado)
        {
            consulta = consulta.Where(r => r.Estado == estado);
        }

        if (filtro.Desde is DateTime desde)
        {
            consulta = consulta.Where(r => r.Fin > desde);
        }

        if (filtro.Hasta is DateTime hasta)
        {
            consulta = consulta.Where(r => r.Inicio < hasta);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(r =>
                EF.Functions.ILike(r.Folio, $"%{texto}%")
                || EF.Functions.ILike(r.Cliente!.RazonSocial, $"%{texto}%")
                || EF.Functions.ILike(r.LugarDescripcion, $"%{texto}%"));
        }

        var total = await consulta.LongCountAsync(ct);

        var filas = await consulta
            .OrderByDescending(r => r.Inicio).ThenByDescending(r => r.Folio)
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(Encabezado())
            .ToListAsync(ct);

        return new Pagina<RentaDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    /// <summary>
    /// DEVUELVE UN ARBOL DE EXPRESION, NO UN DTO.
    ///
    /// Con la forma anterior —<c>.Select(r => Encabezado(r))</c>— EF no sabia traducir la
    /// LLAMADA A METODO y corria la proyeccion EN MEMORIA. Sin <c>Include</c>, las dos
    /// navegaciones obligatorias que se leen aqui —cliente y trabajador— llegaban en nulo y
    /// reventaban con <c>NullReferenceException</c> en cuanto hubiera una renta.
    ///
    /// Como expresion, EF las traduce a JOIN en el mismo SELECT: INNER para las obligatorias y
    /// LEFT para <c>Cotizacion</c>, que es anulable —una renta puede nacer sin cotizacion—.
    ///
    /// <c>LugarRenta</c> se construye dentro del arbol y eso SI se traduce: es un objeto que EF
    /// materializa con columnas de la misma fila, no una llamada a codigo del cliente.
    /// </summary>
    private static Expression<Func<Renta, RentaDto>> Encabezado() => r => new RentaDto(
        r.Id, r.Folio, r.ClienteId, r.Cliente!.RazonSocial,
        r.CotizacionId, r.Cotizacion == null ? null : r.Cotizacion.Folio,
        r.TrabajadorId, r.Trabajador!.Nombre,
        r.Inicio, r.Fin, r.Estado,
        new LugarRenta(
            r.LugarDescripcion, r.LugarCalle, r.LugarColonia, r.LugarMunicipio,
            r.LugarEstadoProv, r.LugarCodigoPostal, r.LugarLatitud, r.LugarLongitud,
            r.LugarContacto, r.LugarTelefono),
        r.Deposito, r.Anticipo, r.Subtotal, r.Descuento, r.Impuestos, r.Total, r.Saldo,
        r.Notas,
        // `Array.Empty` y no `[]`: una EXPRESION DE COLECCION no cabe en un arbol de expresion
        // —error CS9175—. El listado va sin lineas ni conceptos a proposito: son N por
        // documento. El detalle los trae en dos consultas aparte.
        Array.Empty<RentaLineaDto>(), Array.Empty<RentaConceptoDto>());

    public async Task<RentaDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var renta = await bd.Rentas
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(Encabezado())
            .FirstOrDefaultAsync(ct);

        if (renta is null)
        {
            return null;
        }

        var lineas = await bd.RentaLineas
            .AsNoTracking()
            .Where(l => l.RentaId == id)
            .OrderBy(l => l.Orden)
            .Select(l => new RentaLineaDto(
                l.Id, l.EquipoId, l.Equipo!.CodigoInterno, l.Equipo.Modelo!.Nombre,
                l.TarifaId, l.Tarifa!.Nombre,
                l.Cantidad, l.PrecioUnitario, l.HorasIncluidas, l.Importe,
                l.HorometroSalida, l.HorometroDevolucion, l.Orden))
            .ToListAsync(ct);

        var conceptos = await bd.RentaConceptos
            .AsNoTracking()
            .Where(c => c.RentaId == id)
            .Select(c => new RentaConceptoDto(
                c.Id, c.TarifaId, c.Tarifa!.Nombre,
                c.TrabajadorId, c.Trabajador == null ? null : c.Trabajador.Nombre,
                c.Descripcion, c.Cantidad, c.PrecioUnitario, c.Costo, c.Importe))
            .ToListAsync(ct);

        return renta with { Lineas = lineas, Conceptos = conceptos };
    }

    public async Task<IReadOnlyList<ExtensionRentaDto>> ExtensionesAsync(
        Guid id, CancellationToken ct)
        => await bd.ExtensionesRenta
            .AsNoTracking()
            .Where(e => e.RentaId == id)
            .OrderBy(e => e.CreadoEn)
            .Select(e => new ExtensionRentaDto(
                e.Id, e.FinAnterior, e.FinNuevo, e.Motivo,
                e.TrabajadorId, e.Trabajador!.Nombre, e.CreadoEn))
            .ToListAsync(ct);

    public async Task<Resultado<RentaDto>> CrearAsync(AltaRenta alta, CancellationToken ct)
    {
        if (await ValidarAsync(alta, ct) is string invalido)
        {
            return Resultado<RentaDto>.Invalido(invalido);
        }

        var renta = new Renta
        {
            Folio = await folios.SiguienteAsync(TipoDocumento.Renta, ct),
            ClienteId = alta.ClienteId,
            CotizacionId = alta.CotizacionId,
            TrabajadorId = alta.TrabajadorId,
            Inicio = alta.Inicio,
            Fin = alta.Fin,
            Estado = EstadoRenta.Borrador,
            LugarDescripcion = alta.Lugar.Descripcion.Trim(),
            Deposito = alta.Deposito,
            Anticipo = alta.Anticipo,
            Descuento = alta.Descuento,
            Impuestos = alta.Impuestos,
            Notas = Vacio(alta.Notas),
        };

        CopiarLugar(alta.Lugar, renta);
        Recalcular(renta, []);

        bd.Rentas.Add(renta);

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            bd.Entry(renta).State = EntityState.Detached;
            renta.Folio = await folios.SiguienteAsync(TipoDocumento.Renta, ct);
            bd.Rentas.Add(renta);
            await bd.SaveChangesAsync(ct);
        }

        return Resultado<RentaDto>.Ok((await ObtenerAsync(renta.Id, ct))!);
    }

    public async Task<Resultado<RentaDto>> EditarAsync(
        Guid id, AltaRenta cambio, CancellationToken ct)
    {
        if (await ValidarAsync(cambio, ct) is string invalido)
        {
            return Resultado<RentaDto>.Invalido(invalido);
        }

        var renta = await bd.Rentas.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (renta is null)
        {
            return Resultado<RentaDto>.NoEncontrado("La renta no existe.");
        }

        if (renta.Estado != EstadoRenta.Borrador)
        {
            // CONFIRMADA EN ADELANTE NO SE EDITA. Cambiar las fechas aqui dejaria el
            // calendario diciendo un periodo y la renta otro; para eso esta la extension.
            return Resultado<RentaDto>.Conflicto(
                $"La renta esta {renta.Estado} y solo se edita en Borrador. Para alargarla, "
                + "usa la extension.");
        }

        renta.ClienteId = cambio.ClienteId;
        renta.CotizacionId = cambio.CotizacionId;
        renta.TrabajadorId = cambio.TrabajadorId;
        renta.Inicio = cambio.Inicio;
        renta.Fin = cambio.Fin;
        renta.LugarDescripcion = cambio.Lugar.Descripcion.Trim();
        renta.Deposito = cambio.Deposito;
        renta.Anticipo = cambio.Anticipo;
        renta.Descuento = cambio.Descuento;
        renta.Impuestos = cambio.Impuestos;
        renta.Notas = Vacio(cambio.Notas);
        renta.ActualizadoEn = DateTime.UtcNow;

        CopiarLugar(cambio.Lugar, renta);
        Recalcular(renta, await ImportesAsync(id, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado<RentaDto>.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<Resultado<RentaLineaDto>> AgregarLineaAsync(
        Guid rentaId, AltaRentaLinea linea, CancellationToken ct)
    {
        var renta = await bd.Rentas.FirstOrDefaultAsync(r => r.Id == rentaId, ct);

        if (renta is null)
        {
            return Resultado<RentaLineaDto>.NoEncontrado("La renta no existe.");
        }

        if (renta.Estado != EstadoRenta.Borrador)
        {
            return Resultado<RentaLineaDto>.Conflicto(
                $"La renta esta {renta.Estado}: sus lineas ya tienen calendario detras y no "
                + "se tocan.");
        }

        if (linea.Cantidad <= 0 || linea.PrecioUnitario < 0)
        {
            return Resultado<RentaLineaDto>.Invalido(
                "La cantidad tiene que ser mayor que cero y el precio no puede ser negativo.");
        }

        var equipo = await bd.Equipos
            .Where(e => e.Id == linea.EquipoId && e.EliminadoEn == null)
            .Select(e => new { e.CodigoInterno, e.Estado, e.Proposito, e.Horometro })
            .FirstOrDefaultAsync(ct);

        if (equipo is null)
        {
            return Resultado<RentaLineaDto>.Invalido("El equipo no existe.");
        }

        if (equipo.Proposito == Dominio.Activos.PropositoEquipo.Venta)
        {
            return Resultado<RentaLineaDto>.Invalido(
                $"El equipo {equipo.CodigoInterno} esta marcado solo para venta.");
        }

        if (!await bd.Tarifas.AnyAsync(t => t.Id == linea.TarifaId && t.Activo, ct))
        {
            return Resultado<RentaLineaDto>.Invalido(
                "La tarifa no existe o esta retirada.");
        }

        // El UNIQUE renta_linea_unica es (renta, equipo, tarifa). Se comprueba antes para dar
        // el mensaje bueno; el motor es el que lo garantiza.
        if (await bd.RentaLineas.AnyAsync(
                l => l.RentaId == rentaId
                     && l.EquipoId == linea.EquipoId
                     && l.TarifaId == linea.TarifaId,
                ct))
        {
            return Resultado<RentaLineaDto>.Conflicto(
                $"El equipo {equipo.CodigoInterno} ya tiene esa tarifa en esta renta.");
        }

        var nueva = new RentaLinea
        {
            RentaId = rentaId,
            EquipoId = linea.EquipoId,
            TarifaId = linea.TarifaId,
            Cantidad = linea.Cantidad,
            PrecioUnitario = linea.PrecioUnitario,
            HorasIncluidas = linea.HorasIncluidas,
            Importe = linea.Cantidad * linea.PrecioUnitario,
            // El horometro de salida se toma del equipo AL AGREGAR LA LINEA, no al confirmar:
            // es la lectura con la que sale, y si el equipo se usa entre tanto la de confirmar
            // ya no seria la misma.
            HorometroSalida = equipo.Horometro,
            Orden = linea.Orden,
        };

        bd.RentaLineas.Add(nueva);

        await bd.SaveChangesAsync(ct);

        Recalcular(renta, await ImportesAsync(rentaId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado<RentaLineaDto>.Ok(await LineaAsync(nueva.Id, ct));
    }

    public async Task<Resultado> QuitarLineaAsync(
        Guid rentaId, Guid lineaId, CancellationToken ct)
    {
        var renta = await bd.Rentas.FirstOrDefaultAsync(r => r.Id == rentaId, ct);

        if (renta is null)
        {
            return Resultado.NoEncontrado("La renta no existe.");
        }

        if (renta.Estado != EstadoRenta.Borrador)
        {
            return Resultado.Conflicto(
                $"La renta esta {renta.Estado}: sus lineas ya no se tocan.");
        }

        var linea = await bd.RentaLineas
            .FirstOrDefaultAsync(l => l.Id == lineaId && l.RentaId == rentaId, ct);

        if (linea is null)
        {
            return Resultado.NoEncontrado("La linea no existe.");
        }

        bd.RentaLineas.Remove(linea);

        await bd.SaveChangesAsync(ct);

        Recalcular(renta, await ImportesAsync(rentaId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado<RentaConceptoDto>> AgregarConceptoAsync(
        Guid rentaId, AltaRentaConcepto concepto, CancellationToken ct)
    {
        var renta = await bd.Rentas.FirstOrDefaultAsync(r => r.Id == rentaId, ct);

        if (renta is null)
        {
            return Resultado<RentaConceptoDto>.NoEncontrado("La renta no existe.");
        }

        // LOS CONCEPTOS SI SE PUEDEN AGREGAR DESPUES DE CONFIRMAR, y esa es la diferencia con
        // las lineas: un flete extra o una maniobra imprevista aparecen con la renta ya en
        // marcha, y no tocan el calendario de ningun equipo.
        if (renta.Estado is EstadoRenta.Cerrada or EstadoRenta.Cancelada)
        {
            return Resultado<RentaConceptoDto>.Conflicto(
                $"La renta esta {renta.Estado} y ya no admite cargos.");
        }

        if (concepto.Cantidad <= 0 || concepto.PrecioUnitario < 0 || concepto.Costo < 0)
        {
            return Resultado<RentaConceptoDto>.Invalido(
                "Cantidad mayor que cero, y precio y costo no negativos.");
        }

        if (!await bd.Tarifas.AnyAsync(t => t.Id == concepto.TarifaId && t.Activo, ct))
        {
            return Resultado<RentaConceptoDto>.Invalido(
                "La tarifa no existe o esta retirada.");
        }

        if (concepto.TrabajadorId is Guid trabajador
            && !await bd.Trabajadores.AnyAsync(t => t.Id == trabajador, ct))
        {
            return Resultado<RentaConceptoDto>.Invalido("El trabajador no existe.");
        }

        var nuevo = new RentaConcepto
        {
            RentaId = rentaId,
            TarifaId = concepto.TarifaId,
            // EL OPERADOR ES ESTO: un concepto con tarifa de operador y el trabajador que va.
            // Solo quien va y cuanto se cobra; sin jornadas ni horas extra.
            TrabajadorId = concepto.TrabajadorId,
            Descripcion = Vacio(concepto.Descripcion),
            Cantidad = concepto.Cantidad,
            PrecioUnitario = concepto.PrecioUnitario,
            Costo = concepto.Costo,
            Importe = concepto.Cantidad * concepto.PrecioUnitario,
        };

        bd.RentaConceptos.Add(nuevo);

        await bd.SaveChangesAsync(ct);

        Recalcular(renta, await ImportesAsync(rentaId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado<RentaConceptoDto>.Ok(await ConceptoAsync(nuevo.Id, ct));
    }

    public async Task<Resultado> QuitarConceptoAsync(
        Guid rentaId, Guid conceptoId, CancellationToken ct)
    {
        var renta = await bd.Rentas.FirstOrDefaultAsync(r => r.Id == rentaId, ct);

        if (renta is null)
        {
            return Resultado.NoEncontrado("La renta no existe.");
        }

        if (renta.Estado is EstadoRenta.Cerrada or EstadoRenta.Cancelada)
        {
            return Resultado.Conflicto($"La renta esta {renta.Estado}.");
        }

        var concepto = await bd.RentaConceptos
            .FirstOrDefaultAsync(c => c.Id == conceptoId && c.RentaId == rentaId, ct);

        if (concepto is null)
        {
            return Resultado.NoEncontrado("El concepto no existe.");
        }

        bd.RentaConceptos.Remove(concepto);

        await bd.SaveChangesAsync(ct);

        Recalcular(renta, await ImportesAsync(rentaId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado<RentaDto>> CambiarEstadoAsync(
        Guid id, EstadoRenta estado, CancellationToken ct)
    {
        if (!Enum.IsDefined(estado))
        {
            return Resultado<RentaDto>.Invalido("El estado no es valido.");
        }

        var renta = await bd.Rentas.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (renta is null)
        {
            return Resultado<RentaDto>.NoEncontrado("La renta no existe.");
        }

        if (renta.Estado == estado)
        {
            return Resultado<RentaDto>.Ok((await ObtenerAsync(id, ct))!);
        }

        if (!Transiciones.TryGetValue(renta.Estado, out var permitidos)
            || !permitidos.Contains(estado))
        {
            return Resultado<RentaDto>.Conflicto(
                $"No se puede pasar de {renta.Estado} a {estado}.");
        }

        // CONFIRMAR EXIGE LINEAS: una renta sin equipos no ocupa nada y no es una renta.
        if (estado == EstadoRenta.Confirmada
            && !await bd.RentaLineas.AnyAsync(l => l.RentaId == id, ct))
        {
            return Resultado<RentaDto>.Conflicto(
                "No se puede confirmar una renta sin equipos.");
        }

        renta.Estado = estado;
        renta.ActualizadoEn = DateTime.UtcNow;

        await bd.SaveChangesAsync(ct);

        return Resultado<RentaDto>.Ok((await ObtenerAsync(id, ct))!);
    }

    public Task<DatosParaOcupar?> DatosParaOcuparAsync(Guid id, CancellationToken ct)
        => bd.Rentas
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new DatosParaOcupar(
                r.Id, r.Folio, r.Estado, r.Inicio, r.Fin,
                bd.RentaLineas.Where(l => l.RentaId == r.Id).Select(l => l.EquipoId).ToList()))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado> MoverFinAsync(Guid id, DateTime finNuevo, CancellationToken ct)
    {
        var renta = await bd.Rentas.FirstOrDefaultAsync(r => r.Id == id, ct);

        if (renta is null)
        {
            return Resultado.NoEncontrado("La renta no existe.");
        }

        renta.Fin = finNuevo;
        renta.ActualizadoEn = DateTime.UtcNow;

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado<ExtensionRentaDto>> RegistrarExtensionAsync(
        Guid id, AltaExtension alta, CancellationToken ct)
    {
        var renta = await bd.Rentas
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.Fin, r.Estado })
            .FirstOrDefaultAsync(ct);

        if (renta is null)
        {
            return Resultado<ExtensionRentaDto>.NoEncontrado("La renta no existe.");
        }

        if (renta.Estado is not (EstadoRenta.Confirmada or EstadoRenta.Activa))
        {
            return Resultado<ExtensionRentaDto>.Conflicto(
                $"Solo se extiende una renta Confirmada o Activa; esta esta {renta.Estado}.");
        }

        // El CHECK extension_avanza exige fin_nuevo > fin_anterior.
        if (alta.FinNuevo <= renta.Fin)
        {
            return Resultado<ExtensionRentaDto>.Invalido(
                "La extension tiene que ir mas alla del fin actual.");
        }

        if (!await bd.Trabajadores.AnyAsync(t => t.Id == alta.TrabajadorId, ct))
        {
            return Resultado<ExtensionRentaDto>.Invalido("El trabajador no existe.");
        }

        var extension = new ExtensionRenta
        {
            RentaId = id,
            FinAnterior = renta.Fin,
            FinNuevo = alta.FinNuevo,
            Motivo = Vacio(alta.Motivo),
            TrabajadorId = alta.TrabajadorId,
        };

        bd.ExtensionesRenta.Add(extension);

        await bd.SaveChangesAsync(ct);

        return Resultado<ExtensionRentaDto>.Ok((await bd.ExtensionesRenta
            .AsNoTracking()
            .Where(e => e.Id == extension.Id)
            .Select(e => new ExtensionRentaDto(
                e.Id, e.FinAnterior, e.FinNuevo, e.Motivo,
                e.TrabajadorId, e.Trabajador!.Nombre, e.CreadoEn))
            .FirstAsync(ct)));
    }

    public async Task<Resultado> RegistrarDevolucionAsync(
        Guid id, CierreDeRenta cierre, CancellationToken ct)
    {
        var lineas = await bd.RentaLineas.Where(l => l.RentaId == id).ToListAsync(ct);

        if (lineas.Count == 0)
        {
            return Resultado.NoEncontrado("La renta no tiene lineas.");
        }

        if (cierre.HorometrosDevolucion is { Count: > 0 } lecturas)
        {
            foreach (var linea in lineas)
            {
                if (!lecturas.TryGetValue(linea.EquipoId, out var lectura))
                {
                    continue;
                }

                if (linea.HorometroSalida is decimal salida && lectura < salida)
                {
                    return Resultado.Invalido(
                        "El horometro de devolucion no puede ser menor que el de salida.");
                }

                linea.HorometroDevolucion = lectura;

                // La lectura tambien actualiza el equipo: es la ultima que se conoce, y el
                // mantenimiento preventivo de la Fase 3 la va a necesitar.
                var equipo = await bd.Equipos.FirstOrDefaultAsync(e => e.Id == linea.EquipoId, ct);

                if (equipo is not null)
                {
                    equipo.Horometro = lectura;
                    equipo.ActualizadoEn = DateTime.UtcNow;
                }
            }
        }

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    private Task<List<decimal>> ImportesAsync(Guid rentaId, CancellationToken ct)
        => bd.RentaLineas
            .Where(l => l.RentaId == rentaId)
            .Select(l => l.Importe)
            .Concat(bd.RentaConceptos
                .Where(c => c.RentaId == rentaId)
                .Select(c => c.Importe))
            .ToListAsync(ct);

    /// <summary>
    /// EL SUBTOTAL SUMA LAS DOS TABLAS: lineas y conceptos. Es la respuesta a «que se le
    /// cobra», que es distinta de «que equipos van» —solo lineas—.
    ///
    /// El saldo es total menos anticipo. Los pagos son M19 y Fase 4, asi que por ahora el
    /// anticipo es lo unico que descuenta.
    /// </summary>
    private static void Recalcular(Renta renta, List<decimal> importes)
    {
        renta.Subtotal = importes.Sum();
        renta.Total = Math.Max(0, renta.Subtotal - renta.Descuento + renta.Impuestos);
        renta.Saldo = Math.Max(0, renta.Total - renta.Anticipo);
    }

    private static void CopiarLugar(LugarRenta lugar, Renta renta)
    {
        renta.LugarCalle = Vacio(lugar.Calle);
        renta.LugarColonia = Vacio(lugar.Colonia);
        renta.LugarMunicipio = Vacio(lugar.Municipio);
        renta.LugarEstadoProv = Vacio(lugar.EstadoProv);
        renta.LugarCodigoPostal = Vacio(lugar.CodigoPostal);
        renta.LugarLatitud = lugar.Latitud;
        renta.LugarLongitud = lugar.Longitud;
        renta.LugarContacto = Vacio(lugar.Contacto);
        renta.LugarTelefono = Vacio(lugar.Telefono);
    }

    private Task<RentaLineaDto> LineaAsync(Guid id, CancellationToken ct)
        => bd.RentaLineas
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new RentaLineaDto(
                l.Id, l.EquipoId, l.Equipo!.CodigoInterno, l.Equipo.Modelo!.Nombre,
                l.TarifaId, l.Tarifa!.Nombre,
                l.Cantidad, l.PrecioUnitario, l.HorasIncluidas, l.Importe,
                l.HorometroSalida, l.HorometroDevolucion, l.Orden))
            .FirstAsync(ct);

    private Task<RentaConceptoDto> ConceptoAsync(Guid id, CancellationToken ct)
        => bd.RentaConceptos
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new RentaConceptoDto(
                c.Id, c.TarifaId, c.Tarifa!.Nombre,
                c.TrabajadorId, c.Trabajador == null ? null : c.Trabajador.Nombre,
                c.Descripcion, c.Cantidad, c.PrecioUnitario, c.Costo, c.Importe))
            .FirstAsync(ct);

    private async Task<string?> ValidarAsync(AltaRenta alta, CancellationToken ct)
    {
        // El CHECK renta_lugar_no_vacio lo exige en la base. Es la mitad de la decision de
        // quitar la tabla obra: sin descripcion, una maquina que nadie sabe donde esta.
        if (string.IsNullOrWhiteSpace(alta.Lugar.Descripcion))
        {
            return "El lugar de trabajo es obligatorio: donde va a estar la maquina.";
        }

        if (alta.Fin <= alta.Inicio)
        {
            return "El fin de la renta tiene que ser posterior al inicio.";
        }

        if (alta.Deposito < 0 || alta.Anticipo < 0
            || alta.Descuento < 0 || alta.Impuestos < 0)
        {
            return "Los montos no pueden ser negativos.";
        }

        if ((alta.Lugar.Latitud is null) != (alta.Lugar.Longitud is null))
        {
            return "La latitud y la longitud van juntas: las dos o ninguna.";
        }

        var cliente = await bd.Clientes
            .Where(c => c.Id == alta.ClienteId)
            .Select(c => (EstadoCliente?)c.Estado)
            .FirstOrDefaultAsync(ct);

        if (cliente is null)
        {
            return "El cliente no existe.";
        }

        if (cliente != EstadoCliente.Activo)
        {
            return $"El cliente esta {cliente} y no se le puede rentar.";
        }

        if (!await bd.Trabajadores.AnyAsync(t => t.Id == alta.TrabajadorId, ct))
        {
            return "El trabajador no existe.";
        }

        if (alta.CotizacionId is Guid cotizacion
            && !await bd.Cotizaciones.AnyAsync(c => c.Id == cotizacion, ct))
        {
            return "La cotizacion no existe.";
        }

        return null;
    }

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
