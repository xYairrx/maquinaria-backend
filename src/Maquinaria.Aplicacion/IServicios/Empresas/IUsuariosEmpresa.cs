using Maquinaria.Dominio.Seguridad;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Acceso a los usuarios DENTRO de la base de una empresa. Todas las operaciones
/// asumen que el tenant de la peticion ya esta establecido.
/// </summary>
public interface IUsuariosEmpresa
{
    // ---------------------------------------------------------- invitacion --
    /// <summary>
    /// Busca un token por su hash, exigiendo que este vigente: sin usar, sin invalidar
    /// y sin caducar. Devuelve tambien el usuario, que es lo que necesita la pantalla
    /// para decir a quien va dirigida la liga.
    /// </summary>
    Task<TokenConUsuario?> BuscarTokenVigenteAsync(
        string hashToken, PropositoToken proposito, CancellationToken ct);

    /// <summary>
    /// Guarda la contrasena, activa al usuario y QUEMA el token, todo en una
    /// transaccion. Si se quemara por separado y algo fallara, la liga quedaria
    /// consumida sin que la persona pueda entrar.
    /// </summary>
    Task AceptarInvitacionAsync(
        Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct);

    // ----------------------------------------------------- restablecimiento --
    /// <summary>
    /// Invalida los tokens pendientes DEL MISMO PROPOSITO y emite el nuevo, en una
    /// transaccion.
    ///
    /// Las dos cosas van juntas por lo mismo que en el sembrador de administradores: si
    /// se invalidara sin emitir, la persona se queda sin liga y sin saberlo; si se
    /// emitiera sin invalidar, quedan dos ligas validas circulando y la vieja —la que
    /// pudo haber visto quien intercepto el correo— sigue abriendo la cuenta.
    ///
    /// Solo del mismo proposito: pedir un restablecimiento NO debe cancelar la
    /// invitacion pendiente de alguien, que es otro flujo con otra vigencia.
    /// </summary>
    Task EmitirTokenAsync(
        Guid usuarioId, PropositoToken proposito, string hashToken, DateTime expiraEn,
        CancellationToken ct);

    /// <summary>
    /// Guarda la contrasena nueva, QUEMA el token y REVOCA TODAS LAS SESIONES DE
    /// REFRESCO del usuario, en una transaccion.
    ///
    /// La revocacion no es un extra: si alguien restablece porque le tomaron la cuenta y
    /// las sesiones del atacante siguen vivas, el restablecimiento no sirvio de nada
    /// —el atacante conserva acceso indefinido rotando su refresh token—. Cambiar la
    /// contrasena sin cerrar sesiones es la mitad de la operacion.
    ///
    /// NO toca el estado del usuario, a diferencia de AceptarInvitacionAsync: quien
    /// restablece ya estaba Activo, y "activar" aqui seria una forma de resucitar a un
    /// suspendido o a uno de baja.
    /// </summary>
    Task RestablecerContrasenaAsync(
        Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct);

    // --------------------------------------------------------------- login --
    Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken ct);

    /// <summary>
    /// Por id, para el refresco: una sesion_refresh guarda usuario_id, no el correo.
    ///
    /// Se lee EN CADA refresco y no se confia en lo que dice el token viejo, porque es lo
    /// que permite que suspender a alguien o darlo de baja le corte el acceso en la
    /// siguiente renovacion en lugar de en 30 dias.
    /// </summary>
    Task<Usuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>
    /// Las claves de permiso de todos los roles del usuario, sin repetir.
    ///
    /// NO aplica la interseccion con los modulos del plan: eso lo hace el caso de uso,
    /// que es quien conoce el tenant. Aqui solo se lee lo que dice la base de la empresa.
    /// </summary>
    Task<IReadOnlyList<string>> PermisosDeAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>
    /// Los roles del usuario, con su codigo y si saltan la verificacion.
    ///
    /// SUSTITUYO a un TieneAccesoTotalAsync que devolvia solo el bool. Cuesta lo mismo
    /// —una consulta— y de paso da los codigos, que la auditoria necesita: sin ellos,
    /// la columna roles de la bitacora no puede responder si una accion paso por el
    /// bypass de acceso_total o por un permiso concedido.
    /// </summary>
    Task<IReadOnlyList<RolEfectivo>> RolesDeAsync(Guid usuarioId, CancellationToken ct);

    Task RegistrarAccesoAsync(
        Guid usuarioId, DateTime cuandoUtc, string? hashNuevo, CancellationToken ct);

    // ------------------------------------------------------------ sesiones --
    Task CrearSesionAsync(SesionRefresh sesion, CancellationToken ct);

    Task<SesionRefresh?> BuscarSesionPorHashAsync(string hashToken, CancellationToken ct);

    /// <summary>Marca la anterior como reemplazada por la nueva, y guarda la nueva.</summary>
    Task RotarSesionAsync(Guid anteriorId, SesionRefresh nueva, CancellationToken ct);

    /// <summary>
    /// Revoca TODAS las sesiones vivas del usuario.
    ///
    /// Se llama al detectar el reuso de un token ya reemplazado, que significa que
    /// alguien lo robo. Se revoca todo y no solo la cadena afectada porque es mas simple
    /// de razonar y mas fuerte: si un token se filtro, no hay motivo para confiar en los
    /// demas de esa persona.
    /// </summary>
    Task RevocarSesionesDeAsync(Guid usuarioId, CancellationToken ct);
}

public readonly record struct TokenConUsuario(TokenAcceso Token, Usuario Usuario);

/// <param name="Codigo">
/// El codigo del rol, no su id: es lo que se congela en la bitacora, que debe leerse
/// sin joins a tablas que pudieron cambiar.
/// </param>
public readonly record struct RolEfectivo(string Codigo, bool AccesoTotal);
