using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Equipos;

/// <summary>
/// La mitad de base de datos del expediente documental: las filas de <c>archivo</c> y
/// <c>equipo_archivo</c>. La mitad de contenido la lleva <see cref="IAlmacenamientoArchivos"/>,
/// y quien las junta es un Proceso.
///
/// Estan separadas porque son dos almacenes distintos que **no comparten transaccion**: un
/// `SaveChanges` no deshace un archivo escrito en el bucket. El Proceso es el que sabe en que
/// orden hacerlo y como limpiar si la segunda mitad falla.
/// </summary>
public interface IServicioDocumentosEquipo
{
    Task<IReadOnlyList<DocumentoEquipoDto>> ListarAsync(Guid equipoId, CancellationToken ct);

    /// <summary>
    /// Crea las dos filas —<c>archivo</c> y <c>equipo_archivo</c>— para un archivo que ya se
    /// guardo en el almacen.
    /// </summary>
    Task<Resultado<DocumentoEquipoDto>> RegistrarAsync(
        Guid equipoId,
        ArchivoGuardado guardado,
        AltaDocumentoEquipo alta,
        Guid? subidoPorId,
        CancellationToken ct);

    /// <summary>
    /// La ruta en el almacen y los datos de presentacion, o nulo si el documento no existe.
    /// </summary>
    Task<RutaDeDocumento?> ObtenerRutaAsync(
        Guid equipoId, Guid documentoId, CancellationToken ct);

    /// <summary>
    /// Borra las dos filas y devuelve la ruta, para que el Proceso pueda borrar el contenido.
    /// El borrado de <c>archivo</c> es logico —tiene <c>eliminado_en</c>—: la fila conserva el
    /// tamano, que es la fuente de la verdad del consumo por empresa.
    /// </summary>
    Task<Resultado<string>> BorrarAsync(Guid equipoId, Guid documentoId, CancellationToken ct);
}

public sealed record RutaDeDocumento(string Ruta, string NombreOriginal, string TipoMime);
