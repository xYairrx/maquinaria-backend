namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Situacion de una cuenta de usuario dentro de una empresa.
///
/// Los usuarios NO SE BORRAN: viven en un estado. Este enum sustituye al par
/// activo + eliminado_en del diseno original, que permitia cuatro combinaciones
/// de las que dos eran basura (activo con eliminado_en puesto, e inactivo sin el,
/// que no distinguia POR QUE no estaba activo).
///
/// Los valores arrancan en 1, no en 0: un enum de C# vale 0 por defecto y 0 no es
/// ninguno de estos estados, asi que cualquier fila con 0 es detectablemente
/// invalida. La migracion agrega un CHECK para que lo haga cumplir Postgres.
/// </summary>
public enum EstadoUsuario : short
{
    /// <summary>
    /// La fila existe, la invitacion esta enviada y la persona todavia no define
    /// su contrasena. NO puede iniciar sesion.
    ///
    /// Es un estado explicito y no una inferencia sobre HashContrasena == null:
    /// el login comprueba un solo campo en lugar de dos columnas y un hash.
    /// </summary>
    Invitado = 1,

    /// <summary>El unico estado que permite iniciar sesion.</summary>
    Activo = 2,

    /// <summary>Decision del administrador. Reversible.</summary>
    Suspendido = 3,

    /// <summary>
    /// Dejo la empresa. NO reversible: la fila se conserva para que la auditoria
    /// siga siendo legible, pero la persona no vuelve por aqui. Si regresa, es una
    /// cuenta nueva.
    /// </summary>
    Baja = 4,
}
