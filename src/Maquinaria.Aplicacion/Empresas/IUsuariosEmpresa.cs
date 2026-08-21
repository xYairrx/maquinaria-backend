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

    // --------------------------------------------------------------- login --
    Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken ct);

    /// <summary>
    /// Las claves de permiso de todos los roles del usuario, sin repetir.
    ///
    /// NO aplica la interseccion con los modulos del plan: eso lo hace el caso de uso,
    /// que es quien conoce el tenant. Aqui solo se lee lo que dice la base de la empresa.
    /// </summary>
    Task<IReadOnlyList<string>> PermisosDeAsync(Guid usuarioId, CancellationToken ct);

    /// <summary>Si alguno de sus roles trae acceso_total, salta la verificacion.</summary>
    Task<bool> TieneAccesoTotalAsync(Guid usuarioId, CancellationToken ct);

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
