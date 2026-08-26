using Maquinaria.Aplicacion.Comercio;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Procesos.Comercio;

/// <summary>
/// Finaliza una venta: **saca los equipos del parque y les cierra el calendario** para que no
/// puedan rentarse despues.
///
/// Es la pieza que conecta la venta de equipo con la garantia de no-traslape: sin cerrar el
/// calendario, una maquina vendida seguiria apareciendo libre y alguien la rentaria.
///
/// **ADAPTACION AL ESQUEMA MIGRADO.** El alcance describe cerrar el calendario con
/// <c>motivo = Venta</c>; <c>MotivoOcupacion</c> no tiene ese valor y el CHECK de la base es
/// <c>BETWEEN 1 AND 6</c>. Asi que se cierra con <c>Bloqueo</c> y una nota que dice de que venta
/// salio — el efecto sobre la disponibilidad es identico, y lo que se pierde es poder distinguir
/// «vendido» de «bloqueado» leyendo solo el motivo. Queda anotado en el plan de la fase.
/// </summary>
public sealed class ProcesoFinalizarOrdenVenta(
    IServicioOrdenesVenta ordenes,
    IServicioOcupacion ocupacion,
    IUnidadDeTrabajo unidad)
{
    public async Task<Resultado<OrdenVentaDto>> EjecutarAsync(
        Guid ordenId, CancellationToken ct)
    {
        var datos = await ordenes.DatosDeVentaAsync(ordenId, ct);

        if (datos is null)
        {
            return Resultado<OrdenVentaDto>.NoEncontrado("La orden no existe.");
        }

        if (datos.Estado != EstadoOrden.Autorizada)
        {
            return Resultado<OrdenVentaDto>.Conflicto(
                $"La orden {datos.Folio} esta {datos.Estado}: solo se finaliza una Autorizada.");
        }

        if (datos.EquipoIds.Count == 0)
        {
            return Resultado<OrdenVentaDto>.Conflicto(
                "La orden no tiene equipos.");
        }

        var desde = DateTime.UtcNow;

        await using var transaccion = await unidad.IniciarAsync(ct);

        foreach (var equipoId in datos.EquipoIds)
        {
            // EL CIERRE ES UNA OCUPACION SIN FIN: bloquea todo lo posterior, que es exactamente
            // lo que se quiere de una maquina que ya no es nuestra.
            //
            // Y si el equipo tiene una renta abierta que se cruza, el EXCLUDE rechaza el cierre
            // y la venta no se finaliza. Es lo correcto: no se puede entregar una maquina que
            // esta en la obra de otro cliente.
            var cerrado = await ocupacion.OcuparAsync(
                new NuevaOcupacion(
                    equipoId,
                    desde,
                    Fin: null,
                    MotivoOcupacion.Bloqueo,
                    ReferenciaId: ordenId,
                    Nota: $"Vendido en la orden {datos.Folio}"),
                ct);

            if (!cerrado.Correcto)
            {
                return new Resultado<OrdenVentaDto>(
                    false, null, cerrado.Razon, cerrado.Motivo);
            }
        }

        var vendidos = await ordenes.MarcarVendidosAsync(ordenId, ct);

        if (!vendidos.Correcto)
        {
            return new Resultado<OrdenVentaDto>(false, null, vendidos.Razon, vendidos.Motivo);
        }

        var finalizada = await ordenes.MarcarFinalizadaAsync(ordenId, ct);

        if (!finalizada.Correcto)
        {
            return finalizada;
        }

        await transaccion.ConfirmarAsync(ct);

        return Resultado<OrdenVentaDto>.Ok((await ordenes.ObtenerAsync(ordenId, ct))!);
    }
}
