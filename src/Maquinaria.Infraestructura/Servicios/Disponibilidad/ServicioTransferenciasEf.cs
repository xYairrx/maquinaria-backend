using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Organizacion;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Disponibilidad;

internal sealed class ServicioTransferenciasEf(ContextoEmpresa bd) : IServicioTransferencias
{
    public async Task<Pagina<TransferenciaDto>> ListarAsync(
        FiltroTransferencias filtro, CancellationToken ct)
    {
        var consulta = bd.TransferenciasEquipo.AsNoTracking();

        if (filtro.EquipoId is Guid equipo)
        {
            consulta = consulta.Where(t => t.EquipoId == equipo);
        }

        if (filtro.UbicacionId is Guid ubicacion)
        {
            // Las dos puntas: quien pregunta por una bodega quiere lo que entro y lo que salio.
            consulta = consulta.Where(t => t.OrigenId == ubicacion || t.DestinoId == ubicacion);
        }

        var total = await consulta.LongCountAsync(ct);

        var filas = await consulta
            .OrderByDescending(t => t.Fecha)
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(t => new TransferenciaDto(
                t.Id, t.EquipoId, t.Equipo!.CodigoInterno,
                t.OrigenId, t.Origen!.Nombre,
                t.DestinoId, t.Destino!.Nombre,
                t.TrabajadorId, t.Trabajador!.Nombre,
                t.Fecha, t.Motivo))
            .ToListAsync(ct);

        return new Pagina<TransferenciaDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    public async Task<Resultado<TransferenciaDto>> RegistrarAsync(
        AltaTransferencia alta, CancellationToken ct)
    {
        var equipo = await bd.Equipos
            .Where(e => e.Id == alta.EquipoId && e.EliminadoEn == null)
            .Select(e => new { e.CodigoInterno, e.UbicacionId, e.Estado })
            .FirstOrDefaultAsync(ct);

        if (equipo is null)
        {
            return Resultado<TransferenciaDto>.NoEncontrado("El equipo no existe.");
        }

        if (equipo.UbicacionId is null)
        {
            // Sin origen no hay traspaso: `transferencia_equipo.origen_id` es NOT NULL. Un
            // equipo sin ubicacion se coloca editandolo, no traspasandolo.
            return Resultado<TransferenciaDto>.Conflicto(
                $"El equipo {equipo.CodigoInterno} no tiene ubicacion: asignasela antes de "
                + "traspasarlo.");
        }

        if (equipo.UbicacionId == alta.DestinoId)
        {
            // El CHECK transferencia_distinta lo impide igual. Aqui con mensaje.
            return Resultado<TransferenciaDto>.Invalido(
                "El equipo ya esta en esa ubicacion.");
        }

        if (equipo.Estado is EstadoEquipo.Vendido or EstadoEquipo.Baja)
        {
            return Resultado<TransferenciaDto>.Conflicto(
                $"El equipo esta {equipo.Estado} y ya no se traspasa.");
        }

        var destino = await bd.Ubicaciones
            .Where(u => u.Id == alta.DestinoId)
            .Select(u => new { u.Nombre, u.Tipo, u.Activo })
            .FirstOrDefaultAsync(ct);

        if (destino is null)
        {
            return Resultado<TransferenciaDto>.Invalido("La ubicacion destino no existe.");
        }

        // El trigger transferencia_exigir_almacenes lo rechaza igual; este mensaje explica por
        // que, que es lo que el usuario necesita para corregir.
        if (destino.Tipo is not (TipoUbicacion.Bodega or TipoUbicacion.Patio))
        {
            return Resultado<TransferenciaDto>.Invalido(
                $"'{destino.Nombre}' es una sucursal: administra y cotiza, no guarda maquinas. "
                + "Un traspaso va de bodega o patio a bodega o patio.");
        }

        if (!destino.Activo)
        {
            return Resultado<TransferenciaDto>.Invalido(
                $"La ubicacion '{destino.Nombre}' esta retirada.");
        }

        if (!await bd.Trabajadores.AnyAsync(t => t.Id == alta.TrabajadorId, ct))
        {
            return Resultado<TransferenciaDto>.Invalido("El trabajador no existe.");
        }

        var transferencia = new TransferenciaEquipo
        {
            EquipoId = alta.EquipoId,
            OrigenId = equipo.UbicacionId.Value,
            DestinoId = alta.DestinoId,
            TrabajadorId = alta.TrabajadorId,
            Fecha = alta.Fecha == default ? DateTime.UtcNow : alta.Fecha,
            Motivo = string.IsNullOrWhiteSpace(alta.Motivo) ? null : alta.Motivo.Trim(),
        };

        bd.TransferenciasEquipo.Add(transferencia);

        await bd.SaveChangesAsync(ct);

        return Resultado<TransferenciaDto>.Ok(
            (await ObtenerAsync(transferencia.Id, ct))!);
    }

    public async Task<Resultado> MoverEquipoAsync(
        Guid equipoId, Guid destinoId, CancellationToken ct)
    {
        var equipo = await bd.Equipos.FirstOrDefaultAsync(e => e.Id == equipoId, ct);

        if (equipo is null)
        {
            return Resultado.NoEncontrado("El equipo no existe.");
        }

        equipo.UbicacionId = destinoId;
        equipo.ActualizadoEn = DateTime.UtcNow;

        // EL ESTADO NO SE TOCA. Pasarlo a EnTraslado exigiria un cierre —«el equipo llego»—
        // que la Fase 1 no tiene: la logistica es M8 y Fase 2. Sin ese cierre, el equipo se
        // quedaria EnTraslado para siempre y desapareceria de la disponibilidad.
        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    private Task<TransferenciaDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.TransferenciasEquipo
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TransferenciaDto(
                t.Id, t.EquipoId, t.Equipo!.CodigoInterno,
                t.OrigenId, t.Origen!.Nombre,
                t.DestinoId, t.Destino!.Nombre,
                t.TrabajadorId, t.Trabajador!.Nombre,
                t.Fecha, t.Motivo))
            .FirstOrDefaultAsync(ct);
}
