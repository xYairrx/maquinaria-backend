namespace Maquinaria.Aplicacion.Comun;

/// <summary>
/// Donde viven los archivos. Detras hay disco en desarrollo y S3 —Cloudflare R2— en la nube, y
/// quien la usa no sabe cual.
///
/// LA RUTA VA SIEMPRE PREFIJADA POR TENANT, y eso no es cosmetica: con una base por empresa el
/// aislamiento de los datos es fisico, pero el bucket es uno solo. El prefijo es lo unico que
/// separa los archivos de un cliente de los de otro, asi que lo compone quien guarda —con el
/// id del tenant que resolvio el middleware— y nunca llega del cuerpo de la peticion.
///
/// <c>archivo.tamano_bytes</c> es la fuente de la verdad del consumo por empresa: R2 no da
/// tamano por prefijo de forma economica, asi que se lleva en la tabla y no consultando el
/// bucket.
/// </summary>
public interface IAlmacenamientoArchivos
{
    Task<ArchivoGuardado> GuardarAsync(SolicitudDeGuardado solicitud, CancellationToken ct);

    /// <summary>
    /// Abre el contenido, o nulo si la ruta no existe. Nulo y no excepcion: un archivo
    /// borrado del almacen con la fila todavia en la tabla es un estado posible —el borrado
    /// no es transaccional entre la base y el bucket— y el llamador lo traduce a un 404.
    /// </summary>
    Task<Stream?> AbrirAsync(string ruta, CancellationToken ct);

    /// <summary>
    /// Borra del almacen. Devuelve si habia algo que borrar, para que el llamador pueda
    /// distinguir «se borro» de «ya no estaba» sin tratar lo segundo como error.
    /// </summary>
    Task<bool> EliminarAsync(string ruta, CancellationToken ct);
}

/// <param name="Prefijo">
/// La carpeta logica dentro del tenant: <c>equipos/{equipoId}</c>. Sin el id del tenant, que
/// lo pone la implementacion.
/// </param>
public sealed record SolicitudDeGuardado(
    Stream Contenido,
    string NombreOriginal,
    string TipoMime,
    string Prefijo);

/// <param name="HashSha256">
/// Del contenido. Sirve para dos cosas que se piden tarde y no se pueden reconstruir: detectar
/// que dos equipos subieron el mismo documento, y comprobar que el archivo no se corrompio.
/// </param>
public sealed record ArchivoGuardado(
    string Ruta,
    string NombreOriginal,
    string TipoMime,
    long TamanoBytes,
    string HashSha256);
