using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Cotizaciones;

/// <summary>
/// Cotizaciones y sus lineas.
///
/// **SOLO SE EDITA EN BORRADOR.** Una cotizacion enviada es un documento que el cliente ya vio;
/// cambiarle las lineas por detras es lo que hace que el precio que recuerda no sea el que el
/// sistema dice. Enviada en adelante, lo unico que se mueve es el estado.
///
/// LOS TOTALES NO SE CAPTURAN: el subtotal sale de sumar las lineas y el total de
/// <c>subtotal - descuento + impuestos</c>. El descuento y los impuestos si se capturan, porque
/// la fase no calcula impuestos.
/// </summary>
public interface IServicioCotizaciones
{
    Task<Pagina<CotizacionDto>> ListarAsync(FiltroCotizaciones filtro, CancellationToken ct);

    Task<CotizacionDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<CotizacionDto>> CrearAsync(AltaCotizacion alta, CancellationToken ct);

    Task<Resultado<CotizacionDto>> EditarAsync(
        Guid id, AltaCotizacion cambio, CancellationToken ct);

    /// <summary>
    /// Mueve el estado. Las transiciones validas estan en el servicio: no cualquier estado
    /// lleva a cualquier otro, y aceptar un salto arbitrario dejaria cotizaciones aceptadas
    /// que vuelven a borrador.
    /// </summary>
    Task<Resultado<CotizacionDto>> CambiarEstadoAsync(
        Guid id, EstadoCotizacion estado, CancellationToken ct);

    Task<Resultado<CotizacionLineaDto>> AgregarLineaAsync(
        Guid cotizacionId, AltaCotizacionLinea linea, CancellationToken ct);

    Task<Resultado> QuitarLineaAsync(Guid cotizacionId, Guid lineaId, CancellationToken ct);
}
