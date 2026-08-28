using Maquinaria.Dominio.Activos;

namespace Maquinaria.Aplicacion.Disponibilidad;

/// <summary>
/// Una fila del calendario fisico de un equipo.
///
/// **Esta tabla es la pieza que sostiene la fase.** La regla «un equipo no puede tener dos
/// rentas traslapadas» no se implementa consultando cinco tablas: todo lo que ocupa un equipo
/// —renta, reserva, mantenimiento, traslado, bloqueo— inserta una fila aqui, y un `EXCLUDE`
/// con indice GiST hace imposible que dos se traslapen.
/// </summary>
/// <param name="Fin">
/// Nulo es «sin fecha de fin»: bloquea todo lo posterior. Es lo correcto para un equipo fuera
/// de servicio y es la razon de que el rango sea semiabierto.
/// </param>
/// <param name="ReferenciaId">
/// A la renta, la orden de venta o la orden de trabajo que la produjo. Sin FK a proposito:
/// apunta a tablas distintas segun el motivo, y una llave foranea no puede cambiar de destino.
/// </param>
public sealed record OcupacionDto(
    Guid Id,
    Guid EquipoId,
    string CodigoInterno,
    DateTime Inicio,
    DateTime? Fin,
    MotivoOcupacion Motivo,
    Guid? ReferenciaId,
    string? Nota,
    bool Activo);

/// <param name="Motivo">
/// Solo Mantenimiento, Reparacion y Bloqueo se capturan a mano. Renta, Reserva y Traslado los
/// pone un Proceso, porque salen de un documento.
/// </param>
public readonly record struct AltaBloqueo(
    Guid EquipoId,
    DateTime Inicio,
    DateTime? Fin,
    MotivoOcupacion Motivo,
    string? Nota);

/// <summary>Lo que un Proceso pide para ocupar el calendario.</summary>
public readonly record struct NuevaOcupacion(
    Guid EquipoId,
    DateTime Inicio,
    DateTime? Fin,
    MotivoOcupacion Motivo,
    Guid? ReferenciaId,
    string? Nota);

/// <summary>
/// Un equipo libre en el periodo consultado, con lo que hace falta para cotizarlo.
/// </summary>
public sealed record EquipoDisponibleDto(
    Guid Id,
    string CodigoInterno,
    string Marca,
    string Modelo,
    Guid TipoEquipoId,
    string TipoEquipo,
    Guid? UbicacionId,
    string? Ubicacion,
    decimal? PrecioRentaDiaria);

/// <summary>
/// El periodo es OBLIGATORIO: «que hay disponible» sin fechas no es una pregunta que esta
/// tabla pueda contestar.
/// </summary>
public sealed record FiltroDisponibilidad : Comun.Filtro
{
    public DateTime Desde { get; init; }

    public DateTime Hasta { get; init; }

    public Guid? TipoEquipoId { get; init; }

    public Guid? UbicacionId { get; init; }

    /// <summary>Para cotizar el precio negociado de ese cliente en lugar del de lista.</summary>
    public Guid? ClienteId { get; init; }
}
