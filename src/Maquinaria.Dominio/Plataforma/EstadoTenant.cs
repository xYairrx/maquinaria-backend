namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Situacion comercial de una empresa suscrita.
///
/// Los valores arrancan en 1, no en 0: un enum de C# vale 0 por defecto y 0 no es
/// ninguno de estos estados, asi que cualquier fila con 0 es detectablemente
/// invalida. La migracion agrega un CHECK para que lo haga cumplir Postgres.
/// </summary>
public enum EstadoTenant : short
{
    /// <summary>Periodo de prueba. Puede operar.</summary>
    Prueba = 1,

    /// <summary>Suscripcion vigente y pagada. Puede operar.</summary>
    Activo = 2,

    /// <summary>
    /// Decision nuestra, tipicamente por falta de pago. NO puede operar.
    /// Distinto de <see cref="EstadoSuscripcion.Vencida"/>, que es el calendario.
    /// </summary>
    Suspendido = 3,

    /// <summary>Baja definitiva. NO puede operar.</summary>
    Cancelado = 4,
}
