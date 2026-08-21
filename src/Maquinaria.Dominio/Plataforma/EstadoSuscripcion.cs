namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Vigencia de una suscripcion.
///
/// Solo <see cref="Prueba"/> y <see cref="Activa"/> cuentan como vigentes, y son
/// los unicos que entran en el constraint parcial de no-traslape de suscripcion
/// (EXCLUDE ... WHERE estado IN (1, 2)). Los otros dos son historial: quedan
/// registrados sin estorbar para crear una suscripcion nueva.
/// </summary>
public enum EstadoSuscripcion : short
{
    /// <summary>Prueba gratuita. Vigente.</summary>
    Prueba = 1,

    /// <summary>Contratada y pagada. Vigente.</summary>
    Activa = 2,

    /// <summary>
    /// El periodo llego a su fin. Es el calendario, no una decision: no
    /// confundir con <see cref="EstadoTenant.Suspendido"/>, que si lo es.
    /// </summary>
    Vencida = 3,

    /// <summary>Terminada antes de su fin natural.</summary>
    Cancelada = 4,
}
