namespace Maquinaria.Infraestructura.Persistencia;

/// <summary>
/// Configuracion del modelo multi-database.
/// </summary>
public sealed class OpcionesMultiTenancy
{
    public const string Seccion = "MultiTenancy";

    /// <summary>
    /// Cuanto vive en cache la resolucion de una empresa.
    ///
    /// Corto a proposito. Es lo que acota el desfase cuando se suspende una empresa o
    /// se le cambia el plan: con varias instancias, cada una tiene su propia cache y
    /// la invalidacion explicita solo alcanza a una.
    /// </summary>
    public int SegundosCacheTenant { get; set; } = 60;

    /// <summary>
    /// Prefijo de los nombres de base de las empresas. El nombre completo es
    /// prefijo + slug con guiones bajos.
    /// </summary>
    public string PrefijoBaseDatos { get; set; } = "maquinaria_";
}
