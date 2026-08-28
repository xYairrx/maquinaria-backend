using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Equipos;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Procesos.Equipos;

/// <summary>
/// Descargar y borrar un documento del expediente. Las dos operaciones tocan la base y el
/// almacen, asi que son Proceso y no Servicio.
///
/// Van juntas en una clase porque comparten exactamente las mismas dos dependencias y ninguna
/// llega a diez lineas; separarlas seria dos archivos para decir lo mismo.
/// </summary>
public sealed class ProcesoDocumentoEquipo(
    IAlmacenamientoArchivos almacenamiento,
    IServicioDocumentosEquipo documentos,
    ILogger<ProcesoDocumentoEquipo> log)
{
    public async Task<Resultado<ContenidoDocumento>> DescargarAsync(
        Guid equipoId, Guid documentoId, CancellationToken ct)
    {
        var ruta = await documentos.ObtenerRutaAsync(equipoId, documentoId, ct);

        if (ruta is null)
        {
            return Resultado<ContenidoDocumento>.NoEncontrado("El documento no existe.");
        }

        var contenido = await almacenamiento.AbrirAsync(ruta.Ruta, ct);

        if (contenido is null)
        {
            // La fila existe y el contenido no. Es el estado que deja un borrado a medias, y
            // se contesta 404 —el documento no se puede entregar— con el log que dice por que,
            // que es distinto de «no existe la fila».
            log.LogWarning(
                "El documento {Documento} apunta a {Ruta}, que no esta en el almacen.",
                documentoId, ruta.Ruta);

            return Resultado<ContenidoDocumento>.NoEncontrado(
                "El contenido del documento no esta disponible.");
        }

        return Resultado<ContenidoDocumento>.Ok(
            new ContenidoDocumento(contenido, ruta.NombreOriginal, ruta.TipoMime));
    }

    /// <summary>
    /// Borra la fila primero y el contenido despues. Al reves, si la base falla, queda una
    /// fila que apunta a un archivo que ya no existe: peor que un archivo huerfano, porque la
    /// pantalla lo sigue ofreciendo.
    /// </summary>
    public async Task<Resultado> BorrarAsync(
        Guid equipoId, Guid documentoId, CancellationToken ct)
    {
        var borrado = await documentos.BorrarAsync(equipoId, documentoId, ct);

        if (!borrado.Correcto)
        {
            return new Resultado(false, borrado.Razon, borrado.Motivo);
        }

        try
        {
            await almacenamiento.EliminarAsync(borrado.Valor!, ct);
        }
        catch (Exception excepcion)
        {
            // La fila ya no esta, asi que para el usuario el documento se borro. Que el
            // contenido siga en el bucket es basura que hay que limpiar, no un fallo que
            // deba devolver error.
            log.LogWarning(
                excepcion,
                "El documento {Documento} se borro de la base y su archivo {Ruta} sigue en "
                + "el almacen.",
                documentoId, borrado.Valor);
        }

        return Resultado.Ok();
    }
}
