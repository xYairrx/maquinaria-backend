using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Equipos;
using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Organizacion;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Equipos;

internal sealed class ServicioEquiposEf(ContextoEmpresa bd) : IServicioEquipos
{
    /// <summary>
    /// Los estados que pone un Proceso y no la captura. Ver la nota de IServicioEquipos.
    /// </summary>
    private static readonly EstadoEquipo[] EstadosDeDocumento =
        [EstadoEquipo.Reservado, EstadoEquipo.Rentado, EstadoEquipo.EnTraslado, EstadoEquipo.Vendido];

    public async Task<Pagina<EquipoDto>> ListarAsync(FiltroEquipos filtro, CancellationToken ct)
    {
        var consulta = bd.Equipos.AsNoTracking();

        // Aqui SI aplica: equipo es una de las tres entidades con eliminado_en.
        if (!filtro.IncluirEliminados)
        {
            consulta = consulta.Where(e => e.EliminadoEn == null);
        }

        if (filtro.UbicacionId is Guid ubicacion)
        {
            consulta = consulta.Where(e => e.UbicacionId == ubicacion);
        }

        if (filtro.TipoEquipoId is Guid tipo)
        {
            consulta = consulta.Where(e => e.TipoEquipoId == tipo);
        }

        if (filtro.ModeloEquipoId is Guid modelo)
        {
            consulta = consulta.Where(e => e.ModeloEquipoId == modelo);
        }

        if (filtro.Estado is EstadoEquipo estado)
        {
            consulta = consulta.Where(e => e.Estado == estado);
        }

        if (filtro.Proposito is PropositoEquipo proposito)
        {
            // RentaYVenta cuenta como las dos: quien filtra «para venta» quiere ver tambien
            // las maquinas que estan a la venta ademas de rentarse.
            consulta = proposito == PropositoEquipo.RentaYVenta
                ? consulta.Where(e => e.Proposito == PropositoEquipo.RentaYVenta)
                : consulta.Where(e => e.Proposito == proposito
                                   || e.Proposito == PropositoEquipo.RentaYVenta);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            // Codigo interno, serie y modelo: las tres formas de identificar una maquina en
            // el patio. La serie tiene indice GIN de trigramas.
            consulta = consulta.Where(e =>
                EF.Functions.ILike(e.CodigoInterno, $"%{texto}%")
                || (e.NumeroSerie != null && EF.Functions.ILike(e.NumeroSerie, $"%{texto}%"))
                || EF.Functions.ILike(e.Modelo!.Nombre, $"%{texto}%"));
        }

        var total = await consulta.LongCountAsync(ct);

        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("modelo", false) => consulta.OrderBy(e => e.Modelo!.Nombre),
            ("modelo", true) => consulta.OrderByDescending(e => e.Modelo!.Nombre),
            ("estado", false) => consulta.OrderBy(e => e.Estado).ThenBy(e => e.CodigoInterno),
            ("estado", true) => consulta.OrderByDescending(e => e.Estado)
                                        .ThenBy(e => e.CodigoInterno),
            (_, true) => consulta.OrderByDescending(e => e.CodigoInterno),
            _ => consulta.OrderBy(e => e.CodigoInterno),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(e => Proyectar(e))
            .ToListAsync(ct);

        return new Pagina<EquipoDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    private static EquipoDto Proyectar(Equipo e) => new(
        e.Id,
        e.CodigoInterno,
        e.ModeloEquipoId,
        e.Modelo!.Marca!.Nombre,
        e.Modelo.Nombre,
        e.TipoEquipoId,
        e.Tipo!.Nombre,
        e.UbicacionId,
        e.Ubicacion == null ? null : e.Ubicacion.Nombre,
        e.NumeroSerie,
        e.Anio,
        e.Estado,
        e.Proposito,
        e.Origen,
        e.FechaAdquisicion,
        e.CostoAdquisicion,
        e.ValorActual,
        e.Horometro,
        e.Kilometraje,
        e.Notas,
        e.Archivos.Count,
        // Precios vigentes: los que no han caducado. Es lo que la pantalla de expediente
        // muestra como «tiene tarifas cargadas» y lo que decide si se puede cotizar.
        e.Tarifas.Count(t => t.VigenciaHasta == null || t.VigenciaHasta > DateTime.UtcNow));

    public Task<EquipoDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Equipos
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => Proyectar(e))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<EquipoDto>> CrearAsync(AltaEquipo alta, CancellationToken ct)
    {
        if (await ValidarAsync(alta, ct) is string invalido)
        {
            return Resultado<EquipoDto>.Invalido(invalido);
        }

        var codigo = alta.CodigoInterno.Trim().ToUpperInvariant();

        if (await bd.Equipos.AnyAsync(e => e.CodigoInterno == codigo, ct))
        {
            return Resultado<EquipoDto>.Conflicto(
                $"Ya existe un equipo con el codigo '{codigo}'.");
        }

        var equipo = new Equipo
        {
            CodigoInterno = codigo,
            ModeloEquipoId = alta.ModeloEquipoId,
            TipoEquipoId = alta.TipoEquipoId,
            UbicacionId = alta.UbicacionId,
            NumeroSerie = Vacio(alta.NumeroSerie),
            Anio = alta.Anio,
            Proposito = alta.Proposito,
            Origen = alta.Origen,
            FechaAdquisicion = alta.FechaAdquisicion,
            CostoAdquisicion = alta.CostoAdquisicion,
            ValorActual = alta.ValorActual,
            Horometro = alta.Horometro,
            Kilometraje = alta.Kilometraje,
            Notas = Vacio(alta.Notas),
        };

        bd.Equipos.Add(equipo);

        return await GuardarAsync(equipo, ct);
    }

    public async Task<Resultado<EquipoDto>> EditarAsync(
        Guid id, AltaEquipo cambio, CancellationToken ct)
    {
        if (await ValidarAsync(cambio, ct) is string invalido)
        {
            return Resultado<EquipoDto>.Invalido(invalido);
        }

        var equipo = await bd.Equipos
            .FirstOrDefaultAsync(e => e.Id == id && e.EliminadoEn == null, ct);

        if (equipo is null)
        {
            return Resultado<EquipoDto>.NoEncontrado("El equipo no existe.");
        }

        var codigo = cambio.CodigoInterno.Trim().ToUpperInvariant();

        if (await bd.Equipos.AnyAsync(e => e.CodigoInterno == codigo && e.Id != id, ct))
        {
            return Resultado<EquipoDto>.Conflicto(
                $"Ya existe otro equipo con el codigo '{codigo}'.");
        }

        // MOVER LA UBICACION DESDE AQUI NO ES UN TRASPASO. El traspaso deja rastro en
        // transferencia_equipo y ocupa el calendario; esto es una correccion de captura. La
        // pantalla de operacion usa el traspaso; esta, el expediente.
        equipo.CodigoInterno = codigo;
        equipo.ModeloEquipoId = cambio.ModeloEquipoId;
        equipo.TipoEquipoId = cambio.TipoEquipoId;
        equipo.UbicacionId = cambio.UbicacionId;
        equipo.NumeroSerie = Vacio(cambio.NumeroSerie);
        equipo.Anio = cambio.Anio;
        equipo.Proposito = cambio.Proposito;
        equipo.Origen = cambio.Origen;
        equipo.FechaAdquisicion = cambio.FechaAdquisicion;
        equipo.CostoAdquisicion = cambio.CostoAdquisicion;
        equipo.ValorActual = cambio.ValorActual;
        equipo.Horometro = cambio.Horometro;
        equipo.Kilometraje = cambio.Kilometraje;
        equipo.Notas = Vacio(cambio.Notas);
        equipo.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(equipo, ct);
    }

    public async Task<Resultado<EquipoDto>> CambiarEstadoAsync(
        Guid id, CambioEstadoEquipo cambio, CancellationToken ct)
    {
        if (!Enum.IsDefined(cambio.Estado))
        {
            return Resultado<EquipoDto>.Invalido("El estado no es valido.");
        }

        if (EstadosDeDocumento.Contains(cambio.Estado))
        {
            return Resultado<EquipoDto>.Invalido(
                $"El estado {cambio.Estado} lo pone la operacion, no la captura: sale de "
                + "confirmar una renta, un traspaso o una venta.");
        }

        var equipo = await bd.Equipos
            .FirstOrDefaultAsync(e => e.Id == id && e.EliminadoEn == null, ct);

        if (equipo is null)
        {
            return Resultado<EquipoDto>.NoEncontrado("El equipo no existe.");
        }

        // NO SE SACA DE CIRCULACION UNA MAQUINA CON CALENDARIO OCUPADO. Ponerla en
        // mantenimiento o fuera de servicio mientras esta rentada dejaria la renta activa
        // sobre un equipo que la pantalla muestra como no disponible.
        if (cambio.Estado != EstadoEquipo.Disponible)
        {
            var ocupada = await bd.OcupacionesEquipo.AnyAsync(
                o => o.EquipoId == id
                     && o.Activo
                     && (o.Fin == null || o.Fin > DateTime.UtcNow),
                ct);

            if (ocupada)
            {
                return Resultado<EquipoDto>.Conflicto(
                    "El equipo tiene el calendario ocupado. Cierra o cancela lo que lo ocupa "
                    + "antes de cambiarle el estado.");
            }
        }

        equipo.Estado = cambio.Estado;
        equipo.ActualizadoEn = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(cambio.Nota))
        {
            // La nota se acumula, no se sobrescribe: es la bitacora del expediente y su
            // historial es justo lo que alguien va a querer leer.
            equipo.Notas = string.IsNullOrWhiteSpace(equipo.Notas)
                ? cambio.Nota.Trim()
                : $"{equipo.Notas}\n{cambio.Nota.Trim()}";
        }

        return await GuardarAsync(equipo, ct);
    }

    public async Task<Resultado> EliminarAsync(Guid id, CancellationToken ct)
    {
        var equipo = await bd.Equipos
            .FirstOrDefaultAsync(e => e.Id == id && e.EliminadoEn == null, ct);

        if (equipo is null)
        {
            return Resultado.NoEncontrado("El equipo no existe.");
        }

        var ocupada = await bd.OcupacionesEquipo.AnyAsync(
            o => o.EquipoId == id && o.Activo && (o.Fin == null || o.Fin > DateTime.UtcNow),
            ct);

        if (ocupada)
        {
            return Resultado.Conflicto(
                "El equipo tiene el calendario ocupado: no se puede eliminar mientras este "
                + "rentado, reservado o en mantenimiento.");
        }

        // BORRADO LOGICO, no fisico: lo referencian lineas de renta, de cotizacion y detalles
        // de orden, y borrarlo de verdad rompe el historial de lo que se cobro.
        equipo.EliminadoEn = DateTime.UtcNow;

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    private async Task<Resultado<EquipoDto>> GuardarAsync(Equipo equipo, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<EquipoDto>.Conflicto(
                $"Ya existe un equipo con el codigo '{equipo.CodigoInterno}' o con ese QR.");
        }
        catch (DbUpdateException excepcion)
            when (excepcion.Estado() == ErroresPostgres.Foranea)
        {
            return Resultado<EquipoDto>.Invalido(
                "El modelo, el tipo o la ubicacion no existen.");
        }

        return Resultado<EquipoDto>.Ok((await ObtenerAsync(equipo.Id, ct))!);
    }

    /// <summary>
    /// Las validaciones que el motor tambien hace, traducidas a mensaje, mas la que el trigger
    /// impone: **un equipo solo puede estar donde se almacena**.
    /// </summary>
    private async Task<string?> ValidarAsync(AltaEquipo alta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alta.CodigoInterno))
        {
            return "El codigo interno es obligatorio.";
        }

        if (!Enum.IsDefined(alta.Proposito) || !Enum.IsDefined(alta.Origen))
        {
            return "El proposito o el origen no son validos.";
        }

        if (alta.Anio is int anio && (anio < 1900 || anio > 2200))
        {
            return "El anio tiene que estar entre 1900 y 2200.";
        }

        if (alta.CostoAdquisicion < 0 || alta.ValorActual < 0)
        {
            return "Los montos no pueden ser negativos.";
        }

        if (alta.Horometro < 0 || alta.Kilometraje < 0)
        {
            return "El horometro y el kilometraje no pueden ser negativos.";
        }

        if (!await bd.ModelosEquipo.AnyAsync(m => m.Id == alta.ModeloEquipoId, ct))
        {
            return "El modelo no existe.";
        }

        if (!await bd.TiposEquipo.AnyAsync(t => t.Id == alta.TipoEquipoId, ct))
        {
            return "El tipo de equipo no existe.";
        }

        if (alta.UbicacionId is Guid ubicacionId)
        {
            var tipo = await bd.Ubicaciones
                .Where(u => u.Id == ubicacionId)
                .Select(u => (TipoUbicacion?)u.Tipo)
                .FirstOrDefaultAsync(ct);

            if (tipo is null)
            {
                return "La ubicacion no existe.";
            }

            // El trigger equipo_exigir_almacen lo rechaza igual, pero con un mensaje del
            // motor. Este dice por que.
            if (tipo is not (TipoUbicacion.Bodega or TipoUbicacion.Patio))
            {
                return "Un equipo solo puede estar en una bodega o en un patio: una sucursal "
                       + "administra y cotiza, no guarda maquinas.";
            }
        }

        return null;
    }

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
