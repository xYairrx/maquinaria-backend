using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Aplicacion.Rentas;

namespace Maquinaria.Aplicacion.Procesos.Rentas;

/// <summary>
/// Alarga una renta: registra la extension, mueve el fin de la renta y mueve el fin de sus
/// ocupaciones.
///
/// **EL `EXCLUDE` REVALIDA LA DISPONIBILIDAD SOLO.** Alargar hasta el 30 cuando otro cliente ya
/// tiene ese equipo del 25 en adelante es un UPDATE que el motor rechaza, sin que este codigo
/// compruebe nada. Es la misma garantia que impide la doble renta, aplicada a un cambio de
/// fechas.
/// </summary>
public sealed class ProcesoExtenderRenta(
    IServicioRentas rentas,
    IServicioOcupacion ocupacion,
    IUnidadDeTrabajo unidad)
{
    public async Task<Resultado<ExtensionRentaDto>> EjecutarAsync(
        Guid rentaId, AltaExtension alta, CancellationToken ct)
    {
        await using var transaccion = await unidad.IniciarAsync(ct);

        // La extension valida el estado y que la fecha avance; devuelve el fin anterior, que es
        // el dato historico que la tabla existe para conservar.
        var extension = await rentas.RegistrarExtensionAsync(rentaId, alta, ct);

        if (!extension.Correcto)
        {
            return extension;
        }

        var movida = await rentas.MoverFinAsync(rentaId, alta.FinNuevo, ct);

        if (!movida.Correcto)
        {
            return new Resultado<ExtensionRentaDto>(false, null, movida.Razon, movida.Motivo);
        }

        var calendario = await ocupacion.MoverFinAsync(rentaId, alta.FinNuevo, ct);

        if (!calendario.Correcto)
        {
            // Aqui es donde el EXCLUDE dice «no cabe». Sin confirmar, la extension registrada y
            // el fin de la renta vuelven atras: la renta se queda como estaba.
            return new Resultado<ExtensionRentaDto>(
                false, null, calendario.Razon, calendario.Motivo);
        }

        await transaccion.ConfirmarAsync(ct);

        return extension;
    }
}
