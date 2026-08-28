using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Dominio.Activos;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Disponibilidad;

/// <summary>
/// El calendario de los equipos, contra <c>ocupacion_equipo</c> y su <c>EXCLUDE</c>.
///
/// EL PREDICADO DE TRASLAPE ES SIEMPRE EL MISMO y aparece tres veces: dos rangos semiabiertos
/// se cruzan si <c>inicio &lt; hastaOtro</c> y <c>(fin es nulo o fin &gt; desdeOtro)</c>. El
/// nulo es «sin fin», asi que bloquea todo lo posterior; es lo que hace que un equipo fuera de
/// servicio no se pueda rentar nunca hasta que alguien lo libere.
/// </summary>
internal sealed class ServicioOcupacionEf(ContextoEmpresa bd) : IServicioOcupacion
{
    /// <summary>Los motivos que puede capturar una persona. Los otros salen de un documento.</summary>
    private static readonly MotivoOcupacion[] Capturables =
        [MotivoOcupacion.Mantenimiento, MotivoOcupacion.Reparacion, MotivoOcupacion.Bloqueo];

    public async Task<IReadOnlyList<OcupacionDto>> CalendarioAsync(
        Guid equipoId, DateTime desde, DateTime? hasta, CancellationToken ct)
    {
        var consulta = bd.OcupacionesEquipo
            .AsNoTracking()
            .Where(o => o.EquipoId == equipoId);

        // Se traen tambien las inactivas: el calendario es historico y «aqui hubo una renta
        // que se cancelo» es informacion que el mostrador usa.
        consulta = hasta is DateTime fin
            ? consulta.Where(o => o.Inicio < fin && (o.Fin == null || o.Fin > desde))
            : consulta.Where(o => o.Fin == null || o.Fin > desde);

        return await consulta
            .OrderBy(o => o.Inicio)
            .Select(o => new OcupacionDto(
                o.Id, o.EquipoId, o.Equipo!.CodigoInterno, o.Inicio, o.Fin,
                o.Motivo, o.ReferenciaId, o.Nota, o.Activo))
            .ToListAsync(ct);
    }

    public async Task<Pagina<EquipoDisponibleDto>> DisponiblesAsync(
        FiltroDisponibilidad filtro, CancellationToken ct)
    {
        var consulta = bd.Equipos
            .AsNoTracking()
            .Where(e => e.EliminadoEn == null)
            // Vendido y Baja no vuelven nunca; FueraDeServicio y EnMantenimiento tienen su
            // propia fila de ocupacion, asi que el filtro de abajo ya los saca. Se excluyen
            // igual porque un equipo puede estar fuera de servicio sin que nadie haya
            // registrado el bloqueo, y ofrecerlo seria peor que no ofrecerlo.
            .Where(e => e.Estado != EstadoEquipo.Vendido
                     && e.Estado != EstadoEquipo.Baja
                     && e.Estado != EstadoEquipo.FueraDeServicio)
            .Where(e => e.Proposito == PropositoEquipo.Renta
                     || e.Proposito == PropositoEquipo.RentaYVenta)
            // EL CORAZON DE LA CONSULTA: ninguna ocupacion activa que se cruce con el periodo.
            // Es un NOT EXISTS que usa el indice GiST parcial de ocupacion_equipo.
            .Where(e => !bd.OcupacionesEquipo.Any(o =>
                o.EquipoId == e.Id
                && o.Activo
                && o.Inicio < filtro.Hasta
                && (o.Fin == null || o.Fin > filtro.Desde)));

        if (filtro.TipoEquipoId is Guid tipo)
        {
            consulta = consulta.Where(e => e.TipoEquipoId == tipo);
        }

        if (filtro.UbicacionId is Guid ubicacion)
        {
            consulta = consulta.Where(e => e.UbicacionId == ubicacion);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(e =>
                EF.Functions.ILike(e.CodigoInterno, $"%{texto}%")
                || EF.Functions.ILike(e.Modelo!.Nombre, $"%{texto}%"));
        }

        var total = await consulta.LongCountAsync(ct);

        var filas = await consulta
            .OrderBy(e => e.Modelo!.Marca!.Nombre).ThenBy(e => e.CodigoInterno)
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(e => new EquipoDisponibleDto(
                e.Id,
                e.CodigoInterno,
                e.Modelo!.Marca!.Nombre,
                e.Modelo.Nombre,
                e.TipoEquipoId,
                e.Tipo!.Nombre,
                e.UbicacionId,
                e.Ubicacion == null ? null : e.Ubicacion.Nombre,
                // EL PRECIO NEGOCIADO GANA SOBRE EL DE LISTA. Se resuelve en la misma consulta
                // ordenando por ClienteId descendente: el del cliente —no nulo— sale primero.
                e.Tarifas
                    .Where(t => t.Tarifa!.AplicaRenta
                             && (t.VigenciaHasta == null || t.VigenciaHasta > filtro.Desde)
                             && t.VigenciaDesde <= filtro.Desde
                             && (t.ClienteId == null
                                 || (filtro.ClienteId != null && t.ClienteId == filtro.ClienteId)))
                    .OrderByDescending(t => t.ClienteId)
                    .Select(t => (decimal?)t.Precio)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new Pagina<EquipoDisponibleDto>(
            filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    public Task<bool> HayTraslapeAsync(
        Guid equipoId, DateTime inicio, DateTime? fin, CancellationToken ct)
        => bd.OcupacionesEquipo.AnyAsync(
            o => o.EquipoId == equipoId
                 && o.Activo
                 && (fin == null || o.Inicio < fin)
                 && (o.Fin == null || o.Fin > inicio),
            ct);

    public async Task<Resultado<OcupacionDto>> OcuparAsync(
        NuevaOcupacion nueva, CancellationToken ct)
    {
        if (!Enum.IsDefined(nueva.Motivo))
        {
            return Resultado<OcupacionDto>.Invalido("El motivo de ocupacion no es valido.");
        }

        // El CHECK ocupacion_periodo exige fin > inicio. Aqui con mensaje.
        if (nueva.Fin is DateTime fin && fin <= nueva.Inicio)
        {
            return Resultado<OcupacionDto>.Invalido(
                "El fin de la ocupacion tiene que ser posterior al inicio.");
        }

        var equipo = await bd.Equipos
            .Where(e => e.Id == nueva.EquipoId && e.EliminadoEn == null)
            .Select(e => new { e.CodigoInterno, e.Estado })
            .FirstOrDefaultAsync(ct);

        if (equipo is null)
        {
            return Resultado<OcupacionDto>.NoEncontrado("El equipo no existe.");
        }

        if (equipo.Estado is EstadoEquipo.Vendido or EstadoEquipo.Baja)
        {
            return Resultado<OcupacionDto>.Conflicto(
                $"El equipo {equipo.CodigoInterno} esta {equipo.Estado} y su calendario ya "
                + "no se ocupa.");
        }

        var ocupacion = new OcupacionEquipo
        {
            EquipoId = nueva.EquipoId,
            Inicio = nueva.Inicio,
            Fin = nueva.Fin,
            Motivo = nueva.Motivo,
            ReferenciaId = nueva.ReferenciaId,
            Nota = string.IsNullOrWhiteSpace(nueva.Nota) ? null : nueva.Nota.Trim(),
        };

        bd.OcupacionesEquipo.Add(ocupacion);

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsTraslape())
        {
            // AQUI ES DONDE LA GARANTIA SE VUELVE UTIL. El EXCLUDE rechazo la fila; sin este
            // catch, el usuario ve un 500 y nadie entiende que la maquina ya estaba tomada.
            //
            // Se relee para poder decir CON QUE choca, que es la diferencia entre «no se pudo»
            // y «esta rentada del 10 al 20». La lectura va despues del rechazo, asi que no
            // hay carrera que valga: la fila que choca ya esta confirmada.
            var choque = await bd.OcupacionesEquipo
                .AsNoTracking()
                .Where(o => o.EquipoId == nueva.EquipoId
                         && o.Activo
                         && (nueva.Fin == null || o.Inicio < nueva.Fin)
                         && (o.Fin == null || o.Fin > nueva.Inicio))
                .OrderBy(o => o.Inicio)
                .Select(o => new { o.Inicio, o.Fin, o.Motivo })
                .FirstOrDefaultAsync(ct);

            var detalle = choque is null
                ? "El periodo choca con otra ocupacion del mismo equipo."
                : $"El equipo {equipo.CodigoInterno} ya esta ocupado por {choque.Motivo} "
                  + $"desde {choque.Inicio:yyyy-MM-dd}"
                  + (choque.Fin is null ? " sin fecha de fin." : $" hasta {choque.Fin:yyyy-MM-dd}.");

            return Resultado<OcupacionDto>.Conflicto(detalle);
        }

        return Resultado<OcupacionDto>.Ok(new OcupacionDto(
            ocupacion.Id, ocupacion.EquipoId, equipo.CodigoInterno, ocupacion.Inicio,
            ocupacion.Fin, ocupacion.Motivo, ocupacion.ReferenciaId, ocupacion.Nota, true));
    }

    public Task<Resultado<OcupacionDto>> BloquearAsync(
        AltaBloqueo alta, CancellationToken ct)
        => Capturables.Contains(alta.Motivo)
            ? OcuparAsync(
                new NuevaOcupacion(
                    alta.EquipoId, alta.Inicio, alta.Fin, alta.Motivo,
                    // SIN REFERENCIA: un bloqueo manual no viene de ningun documento, y por eso
                    // es el unico que se puede liberar desde la pantalla.
                    ReferenciaId: null,
                    alta.Nota),
                ct)
            : Task.FromResult(Resultado<OcupacionDto>.Invalido(
                $"El motivo {alta.Motivo} lo pone un documento. A mano solo se puede "
                + "Mantenimiento, Reparacion o Bloqueo."));

    public async Task<Resultado> MoverFinAsync(
        Guid referenciaId, DateTime finNuevo, CancellationToken ct)
    {
        var ocupaciones = await bd.OcupacionesEquipo
            .Where(o => o.ReferenciaId == referenciaId && o.Activo)
            .ToListAsync(ct);

        if (ocupaciones.Count == 0)
        {
            return Resultado.NoEncontrado(
                "No hay calendario ocupado por ese documento.");
        }

        foreach (var ocupacion in ocupaciones)
        {
            if (finNuevo <= ocupacion.Inicio)
            {
                return Resultado.Invalido(
                    "El fin nuevo tiene que ser posterior al inicio de la ocupacion.");
            }

            ocupacion.Fin = finNuevo;
        }

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsTraslape())
        {
            // UNA EXTENSION SE REVALIDA SOLA: alargar el fin puede pisar la renta siguiente
            // del mismo equipo, y el EXCLUDE lo rechaza sin que este codigo compruebe nada.
            return Resultado.Conflicto(
                "No se puede alargar: el periodo nuevo choca con otra ocupacion del equipo.");
        }

        return Resultado.Ok();
    }

    public async Task<Resultado> LiberarPorReferenciaAsync(
        Guid referenciaId, CancellationToken ct)
    {
        var ocupaciones = await bd.OcupacionesEquipo
            .Where(o => o.ReferenciaId == referenciaId && o.Activo)
            .ToListAsync(ct);

        foreach (var ocupacion in ocupaciones)
        {
            // activo = false, no Remove: el EXCLUDE es parcial —WHERE activo— asi que esto
            // libera el periodo sin perder el historico.
            ocupacion.Activo = false;
        }

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado> LiberarAsync(Guid id, CancellationToken ct)
    {
        var ocupacion = await bd.OcupacionesEquipo.FirstOrDefaultAsync(o => o.Id == id, ct);

        if (ocupacion is null)
        {
            return Resultado.NoEncontrado("La ocupacion no existe.");
        }

        // Solo se liberan a mano las capturables: quitar la ocupacion de una renta desde aqui
        // dejaria la renta activa sobre un equipo que el calendario dice libre.
        if (!Capturables.Contains(ocupacion.Motivo))
        {
            return Resultado.Conflicto(
                $"Esa ocupacion la produjo un documento ({ocupacion.Motivo}). Se libera "
                + "cerrando o cancelando el documento, no desde aqui.");
        }

        ocupacion.Activo = false;

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }
}
