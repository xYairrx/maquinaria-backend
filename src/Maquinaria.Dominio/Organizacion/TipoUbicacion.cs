namespace Maquinaria.Dominio.Organizacion;

/// <summary>
/// Que clase de lugar es una <see cref="Ubicacion"/> dentro de una sucursal.
///
/// La especificacion solo habla de "sucursales y patios", pero en la practica hay
/// bodegas y talleres, y meterlos en una tabla llamada "patio" se lee mal. Un solo
/// tipo cubre los tres y los que falten, sin inventar una tabla por cada uno.
/// </summary>
public enum TipoUbicacion : short
{
    /// <summary>Explanada donde se resguarda el equipo.</summary>
    Patio = 1,

    /// <summary>Techada, para equipo chico, herramienta y accesorios.</summary>
    Bodega = 2,

    /// <summary>Donde se hace el mantenimiento. En la Fase 3 lo usara el taller.</summary>
    Taller = 3,

    Otro = 4,
}
