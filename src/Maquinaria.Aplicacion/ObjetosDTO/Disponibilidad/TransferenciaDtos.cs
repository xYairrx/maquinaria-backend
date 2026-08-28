namespace Maquinaria.Aplicacion.Disponibilidad;

/// <summary>
/// Un traspaso de equipo entre ubicaciones que almacenan.
///
/// **Solo de almacen a almacen** —bodega o patio, nunca desde ni hacia una sucursal— y lo impone
/// un trigger, no este codigo. Una sucursal administra y cotiza; no guarda maquinas.
/// </summary>
public sealed record TransferenciaDto(
    Guid Id,
    Guid EquipoId,
    string CodigoInterno,
    Guid OrigenId,
    string Origen,
    Guid DestinoId,
    string Destino,
    Guid TrabajadorId,
    string Trabajador,
    DateTime Fecha,
    string? Motivo);

/// <param name="Fin">
/// Cuando termina el traslado. **Si viene, el traspaso OCUPA el calendario** con motivo
/// Traslado y el equipo no se puede rentar en ese periodo; si no viene, el traspaso se registra
/// como instantaneo y no toca el calendario.
///
/// Se deja opcional a proposito: cerrar un traslado en curso —pasar el equipo de EnTraslado a
/// Disponible al llegar— es logistica, que es M8 y Fase 2. Sin ese cierre, ocupar el calendario
/// «hasta que llegue» dejaria una ocupacion abierta que nada cerraria.
/// </param>
public readonly record struct AltaTransferencia(
    Guid EquipoId,
    Guid DestinoId,
    Guid TrabajadorId,
    DateTime Fecha,
    DateTime? Fin,
    string? Motivo);

public sealed record FiltroTransferencias : Comun.Filtro
{
    public Guid? EquipoId { get; init; }

    public Guid? UbicacionId { get; init; }
}
