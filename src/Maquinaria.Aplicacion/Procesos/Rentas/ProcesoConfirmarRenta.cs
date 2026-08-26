using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Aplicacion.Rentas;
using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Comercial;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Procesos.Rentas;

/// <summary>
/// **EL PROCESO QUE SOSTIENE LA FASE.** Confirma una renta y ocupa el calendario de sus equipos.
///
/// Inserta **una fila de <c>ocupacion_equipo</c> por <c>renta_linea</c>**: dos equipos, dos filas
/// de calendario. Y si el `EXCLUDE` rechaza una sola, **la transaccion entera se deshace**: no
/// existe una renta a medio confirmar con tres equipos apartados y el cuarto tomado por otro
/// cliente.
///
/// Eso es lo que hace imposible la doble asignacion incluso con dos capturistas dandole al boton
/// al mismo tiempo. Un <c>if (esta libre)</c> en C# no lo lograria: las dos peticiones leerian
/// «libre» y las dos insertarian.
/// </summary>
public sealed class ProcesoConfirmarRenta(
    IServicioRentas rentas,
    IServicioOcupacion ocupacion,
    IUnidadDeTrabajo unidad,
    ILogger<ProcesoConfirmarRenta> log)
{
    public async Task<Resultado<RentaDto>> EjecutarAsync(Guid rentaId, CancellationToken ct)
    {
        var datos = await rentas.DatosParaOcuparAsync(rentaId, ct);

        if (datos is null)
        {
            return Resultado<RentaDto>.NoEncontrado("La renta no existe.");
        }

        if (datos.Estado != EstadoRenta.Borrador)
        {
            return Resultado<RentaDto>.Conflicto(
                $"La renta {datos.Folio} esta {datos.Estado}: solo se confirma desde Borrador.");
        }

        if (datos.EquipoIds.Count == 0)
        {
            return Resultado<RentaDto>.Conflicto(
                "No se puede confirmar una renta sin equipos.");
        }

        await using var transaccion = await unidad.IniciarAsync(ct);

        // El estado primero: si el calendario rechaza, todo esto se deshace igual, y hacerlo
        // antes deja el resto del Proceso leyendo una renta ya confirmada.
        var estado = await rentas.CambiarEstadoAsync(rentaId, EstadoRenta.Confirmada, ct);

        if (!estado.Correcto)
        {
            return estado;
        }

        foreach (var equipoId in datos.EquipoIds)
        {
            var ocupado = await ocupacion.OcuparAsync(
                new NuevaOcupacion(
                    equipoId,
                    datos.Inicio,
                    datos.Fin,
                    MotivoOcupacion.Renta,
                    // LA REFERENCIA ES LA RENTA: es lo que permite liberar todo su calendario
                    // de un golpe al cerrarla o cancelarla, sin recorrer las lineas.
                    ReferenciaId: rentaId,
                    Nota: $"Renta {datos.Folio}"),
                ct);

            if (!ocupado.Correcto)
            {
                log.LogInformation(
                    "Renta {Folio} rechazada: el equipo {Equipo} no esta libre. {Motivo}",
                    datos.Folio, equipoId, ocupado.Motivo);

                // Se sale SIN confirmar la transaccion: el estado vuelve a Borrador y las
                // ocupaciones de los equipos anteriores se deshacen. El mensaje del calendario
                // —que dice con que choca— pasa tal cual al usuario.
                return new Resultado<RentaDto>(false, null, ocupado.Razon, ocupado.Motivo);
            }
        }

        await transaccion.ConfirmarAsync(ct);

        log.LogInformation(
            "Renta {Folio} confirmada con {Equipos} equipos.",
            datos.Folio, datos.EquipoIds.Count);

        return Resultado<RentaDto>.Ok((await rentas.ObtenerAsync(rentaId, ct))!);
    }
}
