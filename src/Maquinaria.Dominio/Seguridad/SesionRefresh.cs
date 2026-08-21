using System.Net;

namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Un refresh token emitido a un usuario, con rotacion.
///
/// Tres decisiones de seguridad, cada una resolviendo algo concreto:
///
/// 1. Se guarda el HASH, no el token. Si alguien lee la base, no obtiene sesiones
///    usables. Mismo criterio que las contrasenas.
/// 2. <see cref="ReemplazadoPorId"/> habilita DETECCION DE REUSO. Cada refresh
///    emite un token nuevo y marca el anterior como reemplazado. Si llega un token
///    ya reemplazado, alguien lo robo: se revoca TODA LA CADENA y se obliga a
///    iniciar sesion de nuevo.
/// 3. Ip y AgenteUsuario permiten mostrarle al usuario sus sesiones activas y
///    cerrarlas.
///
/// NO SE AUDITA. Cada refresh seria una fila en auditoria y el login ya queda
/// registrado; seria ruido en la tabla que nunca se borra.
/// </summary>
public class SesionRefresh
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UsuarioId { get; set; }

    /// <summary>El hash del token, nunca el token. UNIQUE.</summary>
    public required string HashToken { get; set; }

    public DateTime ExpiraEn { get; set; }

    public DateTime? RevocadoEn { get; set; }

    /// <summary>
    /// Apunta a la sesion que sustituyo a esta. Es lo que forma la cadena de
    /// rotacion y lo que permite detectar el reuso de un token robado.
    /// </summary>
    public Guid? ReemplazadoPorId { get; set; }

    /// <summary>
    /// IPAddress y no string: se mapea a inet, que valida el formato, cubre IPv4 e
    /// IPv6 y permite preguntas de red como ip &lt;&lt; '10.0.0.0/8'. Npgsql no admite
    /// mapear string a inet, y de todas formas IPAddress es del BCL, asi que
    /// Maquinaria.Dominio no gana ninguna dependencia de infraestructura.
    /// </summary>
    public IPAddress? Ip { get; set; }

    public string? AgenteUsuario { get; set; }

    public DateTime CreadoEn { get; set; }

    public Usuario? Usuario { get; set; }

    public SesionRefresh? ReemplazadoPor { get; set; }
}
