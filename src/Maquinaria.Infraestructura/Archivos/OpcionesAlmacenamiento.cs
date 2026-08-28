namespace Maquinaria.Infraestructura.Archivos;

/// <summary>
/// Configuracion del almacenamiento de archivos. Misma forma que <c>OpcionesCorreo</c>: el
/// proveedor se elige por configuracion y la implementacion se registra en consecuencia.
/// </summary>
public sealed class OpcionesAlmacenamiento
{
    public const string Seccion = "Archivos";

    /// <summary>
    /// <c>disco</c> o <c>s3</c>. Hoy solo existe <c>disco</c>; <c>s3</c> es la implementacion
    /// que falta y lo dice al arrancar en lugar de fallar en la primera subida.
    /// </summary>
    public string Proveedor { get; set; } = "disco";

    /// <summary>
    /// Carpeta raiz del almacenamiento en disco. Relativa al directorio de trabajo si no es
    /// absoluta, y **fuera del arbol publicado**: no se sirve como estatico.
    /// </summary>
    public string Raiz { get; set; } = "archivos";

    /// <summary>
    /// Tope por archivo. Existe porque el limite de verdad —el de Kestrel— devuelve un 413
    /// generico sin decir cual era el maximo, y porque una foto de obra de 40 MB subida desde
    /// el telefono es un accidente, no un caso de uso.
    /// </summary>
    public long MaximoBytes { get; set; } = 25 * 1024 * 1024;
}
