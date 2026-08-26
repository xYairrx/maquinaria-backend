using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Aplicacion.Rentas;
using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Procesos.Rentas;

/// <summary>
/// Cierra o cancela una renta. Las dos liberan el calendario y por eso van juntas: comparten las
/// mismas tres dependencias y la misma garantia.
///
/// **LIBERAR NO ES BORRAR.** Las filas de <c>ocupacion_equipo</c> se marcan <c>activo = false</c>
/// y se quedan: el `EXCLUDE` es parcial —<c>WHERE activo</c>— asi que el periodo queda libre sin
/// perder el historico de que maquina estuvo donde. Borrar la fila haria imposible contestar
/// «que hizo esta excavadora en marzo».
/// </summary>
public sealed class ProcesoCerrarRenta(
    IServicioRentas rentas,
    IServicioOcupacion ocupacion,
    IUnidadDeTrabajo unidad)
{
    /// <summary>
    /// Cierra una renta ya devuelta —o activa, si se cierra en el mismo acto de la devolucion—:
    /// registra los horometros, pasa a Cerrada y libera el calendario.
    /// </summary>
    public async Task<Resultado<RentaDto>> CerrarAsync(
        Guid rentaId, CierreDeRenta cierre, CancellationToken ct)
    {
        var datos = await rentas.DatosParaOcuparAsync(rentaId, ct);

        if (datos is null)
        {
            return Resultado<RentaDto>.NoEncontrado("La renta no existe.");
        }

        if (datos.Estado is not (EstadoRenta.Activa or EstadoRenta.Devuelta))
        {
            return Resultado<RentaDto>.Conflicto(
                $"La renta {datos.Folio} esta {datos.Estado}: se cierra desde Activa o "
                + "Devuelta.");
        }

        await using var transaccion = await unidad.IniciarAsync(ct);

        // Los horometros de devolucion primero: si vienen mal —menores que la salida— se
        // rechaza sin haber cambiado el estado ni el calendario.
        var devolucion = await rentas.RegistrarDevolucionAsync(rentaId, cierre, ct);

        if (!devolucion.Correcto)
        {
            return new Resultado<RentaDto>(false, null, devolucion.Razon, devolucion.Motivo);
        }

        var estado = await rentas.CambiarEstadoAsync(rentaId, EstadoRenta.Cerrada, ct);

        if (!estado.Correcto)
        {
            return estado;
        }

        var liberado = await ocupacion.LiberarPorReferenciaAsync(rentaId, ct);

        if (!liberado.Correcto)
        {
            return new Resultado<RentaDto>(false, null, liberado.Razon, liberado.Motivo);
        }

        await transaccion.ConfirmarAsync(ct);

        return Resultado<RentaDto>.Ok((await rentas.ObtenerAsync(rentaId, ct))!);
    }

    /// <summary>
    /// Cancela una renta que todavia no arranco. Igual que cerrar pero **sin devolucion**: no
    /// hubo salida, asi que no hay horometros que registrar ni lecturas que actualizar.
    /// </summary>
    public async Task<Resultado<RentaDto>> CancelarAsync(Guid rentaId, CancellationToken ct)
    {
        var datos = await rentas.DatosParaOcuparAsync(rentaId, ct);

        if (datos is null)
        {
            return Resultado<RentaDto>.NoEncontrado("La renta no existe.");
        }

        if (datos.Estado is not (EstadoRenta.Borrador or EstadoRenta.Confirmada))
        {
            // UNA RENTA ACTIVA NO SE CANCELA: la maquina esta en la obra. Se devuelve y se
            // cierra, que es lo que de verdad paso.
            return Resultado<RentaDto>.Conflicto(
                $"La renta {datos.Folio} esta {datos.Estado}: solo se cancela en Borrador o "
                + "Confirmada. Si ya salio el equipo, cierrala.");
        }

        await using var transaccion = await unidad.IniciarAsync(ct);

        var estado = await rentas.CambiarEstadoAsync(rentaId, EstadoRenta.Cancelada, ct);

        if (!estado.Correcto)
        {
            return estado;
        }

        // En Borrador no hay nada que liberar y la llamada no falla: liberar cero filas es un
        // resultado correcto, no un caso especial que haya que distinguir aqui.
        var liberado = await ocupacion.LiberarPorReferenciaAsync(rentaId, ct);

        if (!liberado.Correcto)
        {
            return new Resultado<RentaDto>(false, null, liberado.Razon, liberado.Motivo);
        }

        await transaccion.ConfirmarAsync(ct);

        return Resultado<RentaDto>.Ok((await rentas.ObtenerAsync(rentaId, ct))!);
    }
}
