using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Equipos;

/// <summary>
/// Los precios por equipo.
///
/// LA GARANTIA LA IMPONE EL MOTOR: <c>equipo_tarifa_sin_traslape</c> es un `EXCLUDE` sobre
/// equipo + tarifa + cliente + rango de vigencia, asi que **no pueden existir dos precios
/// vigentes a la vez** para la misma combinacion. Este servicio no lo comprueba con un
/// `if`: bajo concurrencia las dos transacciones leerian «no hay» y las dos insertarian. Lo
/// que hace es traducir el rechazo del motor a un 409 que dice que choca.
///
/// NO HAY EDICION, y es deliberado: un precio aplicado es un hecho con fecha. Corregirlo
/// reescribiria lo que estuvo vigente. Lo que se hace es CERRAR el vigente —ponerle
/// <c>vigencia_hasta</c>— y cargar el nuevo.
/// </summary>
public interface IServicioEquipoTarifas
{
    Task<IReadOnlyList<EquipoTarifaDto>> ListarAsync(
        Guid equipoId, bool soloVigentes, CancellationToken ct);

    Task<Resultado<EquipoTarifaDto>> CrearAsync(
        Guid equipoId, AltaEquipoTarifa alta, CancellationToken ct);

    /// <summary>
    /// Cierra un precio vigente poniendole fecha de fin. Es la unica forma de «cambiar» un
    /// precio: se cierra el viejo y se carga el nuevo.
    /// </summary>
    Task<Resultado<EquipoTarifaDto>> CerrarAsync(
        Guid equipoId, Guid id, DateTime vigenciaHasta, CancellationToken ct);
}
