namespace Maquinaria.Dominio.Activos;

/// <summary>
/// En que situacion esta una maquina AHORA.
///
/// No se confunde con <see cref="OcupacionEquipo"/>: este campo dice el estado actual,
/// esa tabla dice en que periodos esta comprometida. El estado se puede recalcular; el
/// calendario de ocupacion es el que impide rentar dos veces las mismas fechas.
/// </summary>
public enum EstadoEquipo : short
{
    Disponible = 1,

    /// <summary>Comprometido a futuro, todavia en la bodega.</summary>
    Reservado = 2,

    Rentado = 3,

    /// <summary>En camino entre dos ubicaciones, o hacia una obra.</summary>
    EnTraslado = 4,

    /// <summary>Mantenimiento programado.</summary>
    EnMantenimiento = 5,

    /// <summary>Averiado. No se puede comprometer.</summary>
    FueraDeServicio = 6,

    /// <summary>Se vendio. Sale del inventario rentable pero NO de la base.</summary>
    Vendido = 7,

    /// <summary>Baja definitiva: siniestro, desguace.</summary>
    Baja = 8,
}
