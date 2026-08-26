using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Organizacion;

/// <summary>
/// Las personas de la organizacion.
///
/// SIN BORRADO: <c>trabajador</c> no tiene <c>eliminado_en</c> y lo referencian cotizaciones,
/// rentas, traspasos y ordenes. Retirar a alguien es ponerlo en Baja con su fecha, que es
/// ademas el dato que el negocio quiere conservar.
/// </summary>
public interface IServicioTrabajadores
{
    Task<Pagina<TrabajadorDto>> ListarAsync(FiltroTrabajadores filtro, CancellationToken ct);

    Task<TrabajadorDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<TrabajadorDto>> CrearAsync(AltaTrabajador alta, CancellationToken ct);

    Task<Resultado<TrabajadorDto>> EditarAsync(
        Guid id, AltaTrabajador cambio, CancellationToken ct);

    /// <summary>
    /// Cambia el estado. La fecha de baja y el estado Baja van juntos o ninguno: lo exige el
    /// CHECK <c>trabajador_baja_coherente</c>.
    /// </summary>
    Task<Resultado<TrabajadorDto>> CambiarEstadoAsync(
        Guid id, CambioEstadoTrabajador cambio, CancellationToken ct);
}
