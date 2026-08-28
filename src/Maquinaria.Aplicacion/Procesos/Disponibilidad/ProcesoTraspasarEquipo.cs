using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Dominio.Activos;

namespace Maquinaria.Aplicacion.Procesos.Disponibilidad;

/// <summary>
/// Traspasa un equipo de una ubicacion que almacena a otra.
///
/// Compone tres cosas y por eso es un Proceso: registra el traspaso, mueve la ubicacion del
/// equipo y —si el traslado tiene fin— ocupa el calendario.
///
/// **TODO O NADA.** Sin transaccion, un traslado que choca con el calendario dejaria el equipo
/// ya movido de ubicacion con un traspaso registrado y sin ocupacion: la maquina figuraria en
/// la bodega nueva y disponible para rentar el dia que va en el camion.
/// </summary>
public sealed class ProcesoTraspasarEquipo(
    IServicioTransferencias transferencias,
    IServicioOcupacion ocupacion,
    IUnidadDeTrabajo unidad)
{
    public async Task<Resultado<TransferenciaDto>> EjecutarAsync(
        AltaTransferencia alta, CancellationToken ct)
    {
        if (alta.Fin is DateTime fin && fin <= alta.Fecha)
        {
            return Resultado<TransferenciaDto>.Invalido(
                "El fin del traslado tiene que ser posterior a su fecha.");
        }

        await using var transaccion = await unidad.IniciarAsync(ct);

        var registro = await transferencias.RegistrarAsync(alta, ct);

        if (!registro.Correcto)
        {
            return registro;
        }

        var traspaso = registro.Valor!;

        // El calendario solo se ocupa si el traslado tiene fin. Ver la nota de AltaTransferencia:
        // una ocupacion abierta «hasta que llegue» no la cerraria nada en esta fase.
        if (alta.Fin is DateTime hasta)
        {
            var ocupado = await ocupacion.OcuparAsync(
                new NuevaOcupacion(
                    alta.EquipoId,
                    traspaso.Fecha,
                    hasta,
                    MotivoOcupacion.Traslado,
                    ReferenciaId: traspaso.Id,
                    Nota: $"Traslado a {traspaso.Destino}"),
                ct);

            if (!ocupado.Correcto)
            {
                // Se devuelve el rechazo del calendario tal cual —incluido el 409 que dice con
                // que choca— y al salir sin confirmar, el traspaso registrado se deshace.
                return new Resultado<TransferenciaDto>(
                    false, null, ocupado.Razon, ocupado.Motivo);
            }
        }

        var movido = await transferencias.MoverEquipoAsync(alta.EquipoId, alta.DestinoId, ct);

        if (!movido.Correcto)
        {
            return new Resultado<TransferenciaDto>(false, null, movido.Razon, movido.Motivo);
        }

        await transaccion.ConfirmarAsync(ct);

        return Resultado<TransferenciaDto>.Ok(traspaso);
    }
}
