using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Equipos;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Procesos.Equipos;

/// <summary>
/// Sube un documento al expediente de un equipo. **El primer Proceso de la Fase 1.**
///
/// Compone dos almacenes que NO comparten transaccion: el bucket y la base. Ese es todo el
/// motivo de que exista —si fueran uno, esto seria un metodo del servicio—.
///
/// EL ORDEN IMPORTA, y es este: primero el contenido, despues la fila. Al reves quedaria una
/// fila apuntando a una ruta que todavia no existe, y una pantalla que la pide da un 404
/// mientras la subida va en camino. Con este orden, el peor caso es un archivo en el bucket sin
/// fila — y eso lo limpia el `catch`.
/// </summary>
public sealed class ProcesoSubirDocumentoEquipo(
    IAlmacenamientoArchivos almacenamiento,
    IServicioDocumentosEquipo documentos,
    ILogger<ProcesoSubirDocumentoEquipo> log)
{
    public async Task<Resultado<DocumentoEquipoDto>> EjecutarAsync(
        Guid equipoId,
        SolicitudDeGuardado archivo,
        AltaDocumentoEquipo alta,
        Guid? subidoPorId,
        CancellationToken ct)
    {
        var guardado = await almacenamiento.GuardarAsync(
            archivo with { Prefijo = $"equipos/{equipoId}" }, ct);

        try
        {
            var resultado = await documentos.RegistrarAsync(
                equipoId, guardado, alta, subidoPorId, ct);

            if (!resultado.Correcto)
            {
                // El rechazo tambien deja basura: el equipo no existia o el tipo era invalido,
                // pero el archivo ya se escribio.
                await LimpiarAsync(guardado.Ruta, ct);
            }

            return resultado;
        }
        catch
        {
            // Y si revienta la base, igual. Se limpia y se propaga: un fallo de
            // infraestructura no es un rechazo de negocio y tiene que llegar al manejador
            // global.
            await LimpiarAsync(guardado.Ruta, ct);
            throw;
        }
    }

    /// <summary>
    /// El borrado de limpieza NO propaga su propio fallo: si no se puede borrar, lo que
    /// importa es el error original. Queda en el log como archivo huerfano, con su ruta, que
    /// es lo que hace falta para limpiarlo a mano.
    /// </summary>
    private async Task LimpiarAsync(string ruta, CancellationToken ct)
    {
        try
        {
            await almacenamiento.EliminarAsync(ruta, ct);
        }
        catch (Exception excepcion)
        {
            log.LogWarning(
                excepcion,
                "Archivo huerfano en {Ruta}: se subio y su fila no se pudo crear.",
                ruta);
        }
    }
}
