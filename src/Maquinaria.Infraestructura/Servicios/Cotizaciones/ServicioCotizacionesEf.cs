using System.Linq.Expressions;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Cotizaciones;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Dominio.Organizacion;
using Maquinaria.Dominio.Terceros;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Cotizaciones;

internal sealed class ServicioCotizacionesEf(ContextoEmpresa bd, IFolios folios)
    : IServicioCotizaciones
{
    /// <summary>
    /// LAS TRANSICIONES VALIDAS, en un solo sitio.
    ///
    /// Se declara lo permitido y no lo prohibido: una tabla de «desde → hacia» se lee de un
    /// golpe, y agregar un estado obliga a decidir de donde se llega a el en lugar de que
    /// quede alcanzable desde todas partes por omision.
    /// </summary>
    private static readonly Dictionary<EstadoCotizacion, EstadoCotizacion[]> Transiciones = new()
    {
        [EstadoCotizacion.Borrador] =
            [EstadoCotizacion.Enviada, EstadoCotizacion.Cancelada],
        [EstadoCotizacion.Enviada] =
            [EstadoCotizacion.EnRevision, EstadoCotizacion.Aceptada,
             EstadoCotizacion.Rechazada, EstadoCotizacion.Vencida, EstadoCotizacion.Cancelada],
        [EstadoCotizacion.EnRevision] =
            [EstadoCotizacion.Aceptada, EstadoCotizacion.Rechazada,
             EstadoCotizacion.Vencida, EstadoCotizacion.Cancelada],

        // Aceptada NO es terminal: de ahi sale la renta, y si el cliente se echa para atras
        // antes de rentar, se cancela.
        [EstadoCotizacion.Aceptada] = [EstadoCotizacion.Cancelada],

        // Rechazada, Vencida y Cancelada son terminales: no estan en el diccionario.
    };

    public async Task<Pagina<CotizacionDto>> ListarAsync(
        FiltroCotizaciones filtro, CancellationToken ct)
    {
        var consulta = bd.Cotizaciones.AsNoTracking();

        if (filtro.ClienteId is Guid cliente)
        {
            consulta = consulta.Where(c => c.ClienteId == cliente);
        }

        if (filtro.Estado is EstadoCotizacion estado)
        {
            consulta = consulta.Where(c => c.Estado == estado);
        }

        if (filtro.Desde is DateOnly desde)
        {
            consulta = consulta.Where(c => c.Fecha >= desde);
        }

        if (filtro.Hasta is DateOnly hasta)
        {
            consulta = consulta.Where(c => c.Fecha <= hasta);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            // Folio o cliente: es como se busca una cotizacion cuando llama el cliente.
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.Folio, $"%{texto}%")
                || EF.Functions.ILike(c.Cliente!.RazonSocial, $"%{texto}%"));
        }

        var total = await consulta.LongCountAsync(ct);

        // Por folio descendente: lo ultimo capturado es lo que se busca.
        var filas = await consulta
            .OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Folio)
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(Encabezado())
            .ToListAsync(ct);

        return new Pagina<CotizacionDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    /// <summary>
    /// El listado va SIN LINEAS —lista vacia— a proposito: son N por documento y una pantalla
    /// de cincuenta cotizaciones no las pinta. El detalle las trae.
    ///
    /// DEVUELVE UN ARBOL DE EXPRESION, NO UN DTO.
    ///
    /// Con la forma anterior —<c>.Select(c => Encabezado(c))</c>— EF no sabia traducir la
    /// LLAMADA A METODO y corria la proyeccion EN MEMORIA. Sin <c>Include</c>, las TRES
    /// navegaciones que se leen aqui —cliente, ubicacion y trabajador— llegaban en nulo y
    /// reventaban con <c>NullReferenceException</c> en cuanto hubiera una cotizacion.
    ///
    /// Como expresion, EF las traduce a tres JOIN en el mismo SELECT.
    /// </summary>
    private static Expression<Func<Cotizacion, CotizacionDto>> Encabezado() => c => new CotizacionDto(
        c.Id, c.Folio, c.ClienteId, c.Cliente!.RazonSocial,
        c.UbicacionId, c.Ubicacion!.Nombre,
        c.TrabajadorId, c.Trabajador!.Nombre,
        c.Fecha, c.VigenciaHasta, c.Estado,
        c.Subtotal, c.Descuento, c.Impuestos, c.Total, c.Notas,
        // `Array.Empty` y no `[]`: una EXPRESION DE COLECCION no cabe en un arbol de
        // expresion —error CS9175— y esta proyeccion es uno desde que EF tiene que
        // traducirla a SQL. El listado sigue yendo sin lineas: son N por documento.
        Array.Empty<CotizacionLineaDto>());

    public async Task<CotizacionDto?> ObtenerAsync(Guid id, CancellationToken ct)
    {
        var encabezado = await bd.Cotizaciones
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(Encabezado())
            .FirstOrDefaultAsync(ct);

        if (encabezado is null)
        {
            return null;
        }

        var lineas = await bd.CotizacionLineas
            .AsNoTracking()
            .Where(l => l.CotizacionId == id)
            .OrderBy(l => l.Orden)
            .Select(Linea())
            .ToListAsync(ct);

        return encabezado with { Lineas = lineas.Select(l => l.ADto()).ToList() };
    }

    /// <summary>
    /// La proyeccion de una linea, EN DOS PASOS. El mismo reparto que en
    /// <c>ServicioEquipoTarifasEf</c>, y por el mismo motivo.
    ///
    /// EL MOTIVO ES <c>Unidad.ToString()</c>. El nombre de un enum no existe en la base: la
    /// columna guarda el entero, y los rotulos «Hora», «Dia»… solo viven en el CLR. Meterlo en
    /// el arbol lo rompe con «could not be translated».
    ///
    /// Y ROMPE AUNQUE NO HAYA NI UNA LINEA. La traduccion a SQL ocurre ANTES de ejecutar, asi
    /// que la consulta revienta con la tabla vacia igual que con mil filas. Por eso este
    /// defecto no se parece a los de proyeccion por llamada a metodo —esos solo aparecian con
    /// la primera fila—: aqui el 500 salia al CREAR la primera cotizacion, porque
    /// <c>CrearAsync</c> termina llamando a <c>ObtenerAsync</c>. La cotizacion quedaba
    /// guardada y la respuesta era un error, que es la peor combinacion posible.
    ///
    /// Asi que la consulta trae la UNIDAD CRUDA —un entero, que si se traduce— y el rotulo se
    /// resuelve despues, ya en memoria, en <see cref="Fila.ADto"/>.
    /// </summary>
    private static Expression<Func<CotizacionLinea, Fila>> Linea() => l => new Fila(
        l.Id, l.TarifaId, l.Tarifa!.Nombre, l.Tarifa.Unidad,
        l.EquipoId, l.Equipo == null ? null : l.Equipo.CodigoInterno,
        l.TipoEquipoId, l.TipoEquipo == null ? null : l.TipoEquipo.Nombre,
        l.Descripcion, l.Cantidad, l.PrecioUnitario, l.Importe, l.Orden);

    /// <summary>
    /// Lo que devuelve SQL: identico al DTO salvo que <c>Unidad</c> viaja como enum y no como
    /// su nombre. Es interno a este servicio y no sale de aqui.
    /// </summary>
    private sealed record Fila(
        Guid Id,
        Guid TarifaId,
        string Tarifa,
        UnidadTarifa Unidad,
        Guid? EquipoId,
        string? Equipo,
        Guid? TipoEquipoId,
        string? TipoEquipo,
        string? Descripcion,
        decimal Cantidad,
        decimal PrecioUnitario,
        decimal Importe,
        int Orden)
    {
        public CotizacionLineaDto ADto() => new(
            Id, TarifaId, Tarifa, Unidad.ToString(), EquipoId, Equipo,
            TipoEquipoId, TipoEquipo, Descripcion, Cantidad, PrecioUnitario, Importe, Orden);
    }

    public async Task<Resultado<CotizacionDto>> CrearAsync(
        AltaCotizacion alta, CancellationToken ct)
    {
        if (await ValidarAsync(alta, ct) is string invalido)
        {
            return Resultado<CotizacionDto>.Invalido(invalido);
        }

        var cotizacion = new Cotizacion
        {
            Folio = await folios.SiguienteAsync(TipoDocumento.Cotizacion, ct),
            ClienteId = alta.ClienteId,
            UbicacionId = alta.UbicacionId,
            TrabajadorId = alta.TrabajadorId,
            Fecha = alta.Fecha ?? DateOnly.FromDateTime(DateTime.UtcNow),
            VigenciaHasta = alta.VigenciaHasta,
            Estado = EstadoCotizacion.Borrador,
            Descuento = alta.Descuento,
            Impuestos = alta.Impuestos,
            Notas = Vacio(alta.Notas),
        };

        // Nace sin lineas, asi que los totales son el descuento y los impuestos sobre cero.
        Recalcular(cotizacion, []);

        bd.Cotizaciones.Add(cotizacion);

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            // El folio choco: otra alta simultanea gano la carrera. Se reintenta UNA vez con
            // el siguiente numero. Ver la limitacion documentada en IFolios.
            bd.Entry(cotizacion).State = EntityState.Detached;

            cotizacion.Folio = await folios.SiguienteAsync(TipoDocumento.Cotizacion, ct);
            bd.Cotizaciones.Add(cotizacion);

            await bd.SaveChangesAsync(ct);
        }

        return Resultado<CotizacionDto>.Ok((await ObtenerAsync(cotizacion.Id, ct))!);
    }

    public async Task<Resultado<CotizacionDto>> EditarAsync(
        Guid id, AltaCotizacion cambio, CancellationToken ct)
    {
        if (await ValidarAsync(cambio, ct) is string invalido)
        {
            return Resultado<CotizacionDto>.Invalido(invalido);
        }

        var cotizacion = await bd.Cotizaciones.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cotizacion is null)
        {
            return Resultado<CotizacionDto>.NoEncontrado("La cotizacion no existe.");
        }

        if (cotizacion.Estado != EstadoCotizacion.Borrador)
        {
            return Resultado<CotizacionDto>.Conflicto(
                $"La cotizacion esta {cotizacion.Estado} y solo se edita en Borrador.");
        }

        cotizacion.ClienteId = cambio.ClienteId;
        cotizacion.UbicacionId = cambio.UbicacionId;
        cotizacion.TrabajadorId = cambio.TrabajadorId;
        cotizacion.Fecha = cambio.Fecha ?? cotizacion.Fecha;
        cotizacion.VigenciaHasta = cambio.VigenciaHasta;
        cotizacion.Descuento = cambio.Descuento;
        cotizacion.Impuestos = cambio.Impuestos;
        cotizacion.Notas = Vacio(cambio.Notas);
        cotizacion.ActualizadoEn = DateTime.UtcNow;

        Recalcular(cotizacion, await ImportesAsync(id, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado<CotizacionDto>.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<Resultado<CotizacionDto>> CambiarEstadoAsync(
        Guid id, EstadoCotizacion estado, CancellationToken ct)
    {
        if (!Enum.IsDefined(estado))
        {
            return Resultado<CotizacionDto>.Invalido("El estado no es valido.");
        }

        var cotizacion = await bd.Cotizaciones.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cotizacion is null)
        {
            return Resultado<CotizacionDto>.NoEncontrado("La cotizacion no existe.");
        }

        if (cotizacion.Estado == estado)
        {
            // Idempotente: pedir el estado en el que ya esta no es un error.
            return Resultado<CotizacionDto>.Ok((await ObtenerAsync(id, ct))!);
        }

        if (!Transiciones.TryGetValue(cotizacion.Estado, out var permitidos)
            || !permitidos.Contains(estado))
        {
            return Resultado<CotizacionDto>.Conflicto(
                $"No se puede pasar de {cotizacion.Estado} a {estado}.");
        }

        // ENVIAR EXIGE LINEAS. Una cotizacion vacia enviada al cliente es un documento sin
        // contenido, y el estado dejaria de poder editarla para arreglarlo.
        if (estado == EstadoCotizacion.Enviada
            && !await bd.CotizacionLineas.AnyAsync(l => l.CotizacionId == id, ct))
        {
            return Resultado<CotizacionDto>.Conflicto(
                "No se puede enviar una cotizacion sin lineas.");
        }

        cotizacion.Estado = estado;
        cotizacion.ActualizadoEn = DateTime.UtcNow;

        await bd.SaveChangesAsync(ct);

        return Resultado<CotizacionDto>.Ok((await ObtenerAsync(id, ct))!);
    }

    public async Task<Resultado<CotizacionLineaDto>> AgregarLineaAsync(
        Guid cotizacionId, AltaCotizacionLinea linea, CancellationToken ct)
    {
        var cotizacion = await bd.Cotizaciones
            .FirstOrDefaultAsync(c => c.Id == cotizacionId, ct);

        if (cotizacion is null)
        {
            return Resultado<CotizacionLineaDto>.NoEncontrado("La cotizacion no existe.");
        }

        if (cotizacion.Estado != EstadoCotizacion.Borrador)
        {
            return Resultado<CotizacionLineaDto>.Conflicto(
                $"La cotizacion esta {cotizacion.Estado}: sus lineas ya no se tocan.");
        }

        if (linea.Cantidad <= 0)
        {
            return Resultado<CotizacionLineaDto>.Invalido(
                "La cantidad tiene que ser mayor que cero.");
        }

        if (linea.PrecioUnitario < 0)
        {
            return Resultado<CotizacionLineaDto>.Invalido(
                "El precio no puede ser negativo.");
        }

        var tarifa = await bd.Tarifas
            .Where(t => t.Id == linea.TarifaId)
            .Select(t => new { t.Activo, t.AplicaRenta })
            .FirstOrDefaultAsync(ct);

        if (tarifa is null)
        {
            return Resultado<CotizacionLineaDto>.Invalido("La tarifa no existe.");
        }

        if (!tarifa.Activo)
        {
            return Resultado<CotizacionLineaDto>.Invalido(
                "La tarifa esta retirada del catalogo.");
        }

        if (linea.EquipoId is Guid equipoId
            && !await bd.Equipos.AnyAsync(e => e.Id == equipoId && e.EliminadoEn == null, ct))
        {
            return Resultado<CotizacionLineaDto>.Invalido("El equipo no existe.");
        }

        if (linea.TipoEquipoId is Guid tipoId
            && !await bd.TiposEquipo.AnyAsync(t => t.Id == tipoId, ct))
        {
            return Resultado<CotizacionLineaDto>.Invalido("El tipo de equipo no existe.");
        }

        var nueva = new CotizacionLinea
        {
            CotizacionId = cotizacionId,
            TarifaId = linea.TarifaId,
            EquipoId = linea.EquipoId,
            TipoEquipoId = linea.TipoEquipoId,
            Descripcion = Vacio(linea.Descripcion),
            Cantidad = linea.Cantidad,
            PrecioUnitario = linea.PrecioUnitario,
            // EL UNICO CALCULO DE LA FASE: cantidad por precio. Ni escoge tarifa, ni prorratea,
            // ni decide si doce dias son semana mas dias.
            Importe = linea.Cantidad * linea.PrecioUnitario,
            Orden = linea.Orden,
        };

        bd.CotizacionLineas.Add(nueva);

        await bd.SaveChangesAsync(ct);

        Recalcular(cotizacion, await ImportesAsync(cotizacionId, ct));

        await bd.SaveChangesAsync(ct);

        var creada = await bd.CotizacionLineas
            .AsNoTracking()
            .Where(l => l.Id == nueva.Id)
            .Select(Linea())
            .FirstAsync(ct);

        return Resultado<CotizacionLineaDto>.Ok(creada.ADto());
    }

    public async Task<Resultado> QuitarLineaAsync(
        Guid cotizacionId, Guid lineaId, CancellationToken ct)
    {
        var cotizacion = await bd.Cotizaciones
            .FirstOrDefaultAsync(c => c.Id == cotizacionId, ct);

        if (cotizacion is null)
        {
            return Resultado.NoEncontrado("La cotizacion no existe.");
        }

        if (cotizacion.Estado != EstadoCotizacion.Borrador)
        {
            return Resultado.Conflicto(
                $"La cotizacion esta {cotizacion.Estado}: sus lineas ya no se tocan.");
        }

        var linea = await bd.CotizacionLineas
            .FirstOrDefaultAsync(l => l.Id == lineaId && l.CotizacionId == cotizacionId, ct);

        if (linea is null)
        {
            return Resultado.NoEncontrado("La linea no existe.");
        }

        bd.CotizacionLineas.Remove(linea);

        await bd.SaveChangesAsync(ct);

        Recalcular(cotizacion, await ImportesAsync(cotizacionId, ct));

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    /// <summary>
    /// Los importes de las lineas, para recalcular. Se lee de la base y no de una coleccion en
    /// memoria: la cotizacion se carga sin lineas.
    /// </summary>
    private Task<List<decimal>> ImportesAsync(Guid cotizacionId, CancellationToken ct)
        => bd.CotizacionLineas
            .Where(l => l.CotizacionId == cotizacionId)
            .Select(l => l.Importe)
            .ToListAsync(ct);

    /// <summary>
    /// Subtotal = suma de lineas. Total = subtotal - descuento + impuestos.
    ///
    /// Se recalcula en cada cambio y **nunca se acepta desde el cuerpo**: un total capturado a
    /// mano puede no cuadrar con las lineas, y entonces no hay forma de saber cual de los dos
    /// numeros es el bueno.
    /// </summary>
    private static void Recalcular(Cotizacion cotizacion, List<decimal> importes)
    {
        cotizacion.Subtotal = importes.Sum();

        // El total no baja de cero: el CHECK cotizacion_montos lo exige, y un descuento mayor
        // que el subtotal es un dato mal capturado que no debe reventar el guardado.
        cotizacion.Total = Math.Max(
            0, cotizacion.Subtotal - cotizacion.Descuento + cotizacion.Impuestos);
    }

    private async Task<string?> ValidarAsync(AltaCotizacion alta, CancellationToken ct)
    {
        if (alta.Descuento < 0 || alta.Impuestos < 0)
        {
            return "El descuento y los impuestos no pueden ser negativos.";
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
            return $"El cliente esta {cliente} y no se le puede cotizar.";
        }

        var ubicacion = await bd.Ubicaciones
            .Where(u => u.Id == alta.UbicacionId)
            .Select(u => new { u.Nombre, u.Tipo })
            .FirstOrDefaultAsync(ct);

        if (ubicacion is null)
        {
            return "La ubicacion no existe.";
        }

        // LA REGLA DEL MOTOR: una cotizacion solo sale de una ubicacion administrativa. El
        // trigger cotizacion_exigir_administrativa la rechaza igual; esto lo explica.
        if (ubicacion.Tipo is not (TipoUbicacion.Sucursal or TipoUbicacion.Patio))
        {
            return $"'{ubicacion.Nombre}' es una bodega: guarda maquinas y no cotiza. Una "
                   + "cotizacion sale de una sucursal o de un patio.";
        }

        if (!await bd.Trabajadores.AnyAsync(t => t.Id == alta.TrabajadorId, ct))
        {
            return "El trabajador no existe.";
        }

        if (alta.VigenciaHasta is DateOnly vigencia
            && alta.Fecha is DateOnly fecha
            && vigencia < fecha)
        {
            return "La vigencia no puede ser anterior a la fecha de la cotizacion.";
        }

        return null;
    }

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
