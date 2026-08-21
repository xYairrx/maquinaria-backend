namespace Maquinaria.Dominio.Trazabilidad;

/// <summary>
/// Que clase de hecho registra una fila de <see cref="Auditoria"/>.
///
/// La division en dos bloques no es cosmetica: un interceptor de SaveChanges SOLO
/// VE ESCRITURAS. Los tres primeros los escribe el interceptor; los cinco
/// siguientes los escribe el caso de uso a mano, porque no modifican ni una fila.
///
/// Y esos cinco son justo los que mas importa auditar de alguien con acceso total:
/// una exportacion no cambia nada, y es lo que quieres saber que hizo.
///
/// Arranca en 1 y no en 0 por la misma convencion que EstadoTenant: un enum de C#
/// vale 0 por defecto, asi que el 0 es detectablemente invalido y el CHECK lo hace
/// cumplir Postgres.
/// </summary>
public enum AccionAuditoria : short
{
    /// <summary>Fila creada. valores_anteriores va NULL.</summary>
    Alta = 1,

    /// <summary>Fila modificada. Van los dos jsonb, solo con lo que cambio.</summary>
    Cambio = 2,

    /// <summary>
    /// LA FILA DESAPARECIO. valores_nuevos va NULL.
    ///
    /// Se llama Borrado y no Baja a proposito: la baja de un usuario es un cambio
    /// de estado, o sea <see cref="Cambio"/>. Con el nombre viejo, dos cosas
    /// distintas compartian nombre en el campo que se consulta para saber que paso.
    /// </summary>
    Borrado = 3,

    /// <summary>Consulto un expediente. Los dos jsonb van NULL.</summary>
    Acceso = 4,

    /// <summary>
    /// Intento rechazado por permisos. Casi nunca disparara para 'administrador',
    /// que salta la verificacion; existe para los otros ocho roles.
    /// </summary>
    Denegado = 5,

    /// <summary>Se llevo datos.</summary>
    Exportacion = 6,

    Login = 7,

    /// <summary>
    /// Lo que vuelve observable la regla del limite de intentos. entidad_id guarda
    /// el CORREO intentado, que no es ningun uuid: otra razon por la que
    /// entidad_id es text.
    /// </summary>
    LoginFallido = 8,
}
