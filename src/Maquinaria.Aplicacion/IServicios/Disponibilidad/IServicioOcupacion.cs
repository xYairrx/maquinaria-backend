using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Disponibilidad;

/// <summary>
/// El calendario fisico de los equipos. **El unico que escribe en <c>ocupacion_equipo</c>.**
///
/// Que sea el unico no es organizacion, es la condicion para que la garantia signifique algo:
/// si las rentas insertaran sus filas y los traspasos las suyas por su cuenta, cada camino
/// tendria su propia idea de que cuenta como ocupado.
///
/// NUNCA SE PREGUNTA «¿esta libre?» PARA LUEGO INSERTAR. Bajo concurrencia las dos
/// transacciones leerian «libre» y las dos insertarian; el `EXCLUDE` es lo que de verdad lo
/// impide, y este servicio traduce su rechazo a un <see cref="RazonRechazo.Conflicto"/> que
/// dice que choca. <see cref="HayTraslapeAsync"/> existe solo para AVISAR antes en la
/// pantalla, no para decidir.
/// </summary>
public interface IServicioOcupacion
{
    /// <summary>El calendario de un equipo, para pintarlo.</summary>
    Task<IReadOnlyList<OcupacionDto>> CalendarioAsync(
        Guid equipoId, DateTime desde, DateTime? hasta, CancellationToken ct);

    /// <summary>
    /// Los equipos libres en el periodo. Es UNA consulta con el indice GiST, no cinco joins.
    /// </summary>
    Task<Pagina<EquipoDisponibleDto>> DisponiblesAsync(
        FiltroDisponibilidad filtro, CancellationToken ct);

    /// <summary>
    /// Aviso previo, no decision. Ver la nota de la interfaz.
    /// </summary>
    Task<bool> HayTraslapeAsync(
        Guid equipoId, DateTime inicio, DateTime? fin, CancellationToken ct);

    /// <summary>
    /// Ocupa el calendario. Lo llaman los Procesos —confirmar una renta, traspasar un equipo—
    /// y el controlador de bloqueos.
    /// </summary>
    Task<Resultado<OcupacionDto>> OcuparAsync(NuevaOcupacion nueva, CancellationToken ct);

    /// <summary>
    /// La puerta de captura manual. Igual que <see cref="OcuparAsync"/> pero **solo acepta los
    /// motivos capturables** —Mantenimiento, Reparacion y Bloqueo—: los otros salen de un
    /// documento, y dejar que la pantalla inserte una ocupacion de Renta sin renta detras es
    /// exactamente el desajuste que esta tabla existe para evitar.
    /// </summary>
    Task<Resultado<OcupacionDto>> BloquearAsync(AltaBloqueo alta, CancellationToken ct);

    /// <summary>
    /// Mueve el fin de las ocupaciones vigentes de una referencia. Es lo que hace una
    /// extension de renta, y el `EXCLUDE` la revalida sola.
    /// </summary>
    Task<Resultado> MoverFinAsync(
        Guid referenciaId, DateTime finNuevo, CancellationToken ct);

    /// <summary>
    /// Libera lo que ocupaba una referencia poniendo <c>activo = false</c>. **No borra la
    /// fila**: el historico de que estuvo donde y cuando es justo lo que se quiere conservar.
    /// </summary>
    Task<Resultado> LiberarPorReferenciaAsync(Guid referenciaId, CancellationToken ct);

    /// <summary>Libera una ocupacion concreta. Lo usa el controlador de bloqueos.</summary>
    Task<Resultado> LiberarAsync(Guid id, CancellationToken ct);
}
