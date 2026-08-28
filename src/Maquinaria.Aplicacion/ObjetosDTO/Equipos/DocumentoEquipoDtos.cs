using Maquinaria.Dominio.Activos;

namespace Maquinaria.Aplicacion.Equipos;

/// <summary>
/// Un documento del expediente del equipo: foto, factura, poliza, manual, certificado u otro.
///
/// La fila vive en <c>equipo_archivo</c> y el contenido en el almacen. Las dos mitades se crean
/// juntas en <c>ProcesoSubirDocumentoEquipo</c>, que es lo que evita archivos huerfanos en el
/// bucket y filas que apuntan a nada.
/// </summary>
public sealed record DocumentoEquipoDto(
    Guid Id,
    Guid EquipoId,
    Guid ArchivoId,
    TipoArchivoEquipo Tipo,
    string? Descripcion,
    string NombreOriginal,
    string TipoMime,
    long TamanoBytes,
    DateTime CreadoEn);

/// <summary>
/// Lo que acompana al archivo en la subida. El contenido va como <c>multipart/form-data</c>, no
/// aqui: un base64 en JSON crece un tercio y obliga a tener el archivo entero en memoria.
/// </summary>
public readonly record struct AltaDocumentoEquipo(
    TipoArchivoEquipo Tipo,
    string? Descripcion);

/// <summary>
/// El contenido de un documento, para descargarlo.
/// </summary>
public sealed record ContenidoDocumento(
    Stream Contenido,
    string NombreOriginal,
    string TipoMime);
