using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Organizacion;

/// <summary>
/// Bodegas, sucursales y patios.
///
/// EL TIPO SE PUEDE CORREGIR, y tiene una consecuencia que el servicio comprueba: bajar un
/// patio a sucursal le quita la capacidad de almacenar, y si ya tiene equipos encima el
/// trigger <c>equipo_exigir_almacen</c> los dejaria en una ubicacion invalida. Cambiar el tipo
/// a uno que no almacena se rechaza mientras haya equipos ahi.
/// </summary>
public interface IServicioUbicaciones
{
    Task<Pagina<UbicacionDto>> ListarAsync(FiltroUbicaciones filtro, CancellationToken ct);

    Task<UbicacionDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<UbicacionDto>> CrearAsync(AltaUbicacion alta, CancellationToken ct);

    Task<Resultado<UbicacionDto>> EditarAsync(
        Guid id, AltaUbicacion cambio, CancellationToken ct);

    Task<Resultado<UbicacionDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct);
}
