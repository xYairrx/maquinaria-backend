using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Disponibilidad;

/// <summary>
/// El historial de traspasos. **Solo lee y registra la fila**: mover la ubicacion del equipo y
/// ocupar el calendario los hace <c>ProcesoTraspasarEquipo</c>, porque son tres tablas y tienen
/// que ser todo o nada.
/// </summary>
public interface IServicioTransferencias
{
    Task<Pagina<TransferenciaDto>> ListarAsync(
        FiltroTransferencias filtro, CancellationToken ct);

    /// <summary>
    /// Valida y registra la fila de traspaso, y devuelve el origen —la ubicacion en la que
    /// estaba el equipo— porque el Proceso lo necesita y solo aqui se sabe.
    /// </summary>
    Task<Resultado<TransferenciaDto>> RegistrarAsync(
        AltaTransferencia alta, CancellationToken ct);

    /// <summary>Mueve la ubicacion del equipo. Lo llama el Proceso, dentro de su transaccion.</summary>
    Task<Resultado> MoverEquipoAsync(Guid equipoId, Guid destinoId, CancellationToken ct);
}
