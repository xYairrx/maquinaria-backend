namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Una persona que opera el sistema dentro de una empresa. Vive en la base de ESA
/// empresa, no en la central.
///
/// HOMONIMA de Maquinaria.Dominio.Plataforma.Usuario a proposito: son la misma idea en
/// dos mundos separados fisicamente. En SQL no hay colision porque son bases
/// distintas; en C# las distingue el namespace, y confundirlas no compila, porque
/// cada una existe solo en su propio DbContext.
/// </summary>
public class Usuario
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Normalizado a minusculas al escribir, en la capa de aplicacion. UNIQUE.
    ///
    /// El UNIQUE es GLOBAL, no parcial por estado, y eso tiene una consecuencia que
    /// hay que aceptar a ojos abiertos: como los usuarios no se borran, un correo
    /// NUNCA se libera. La alternativa —unico solo entre los que no estan de baja—
    /// volveria ambiguo el login: buscar por correo devolveria varias filas y
    /// habria que filtrar por estado ANTES de validar. Ese filtro, olvidado una
    /// vez, es un agujero de autenticacion.
    /// </summary>
    public required string Correo { get; set; }

    /// <summary>
    /// NULL mientras el estado es <see cref="EstadoUsuario.Invitado"/>: no hay
    /// registro publico, los usuarios se crean por invitacion, y entre que se crea
    /// la cuenta y la persona define su contrasena la fila existe sin hash.
    /// </summary>
    public string? HashContrasena { get; set; }

    public required string Nombre { get; set; }

    public string? Apellidos { get; set; }

    public string? Telefono { get; set; }

    /// <summary>
    /// Sustituye a activo + eliminado_en. Solo <see cref="EstadoUsuario.Activo"/>
    /// puede iniciar sesion.
    /// </summary>
    public EstadoUsuario Estado { get; set; }

    public bool DebeCambiarContrasena { get; set; }

    public DateTime? UltimoAccesoEn { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }

    public ICollection<UsuarioRol> Roles { get; } = [];

    // FALTA cliente_id, y es a proposito. El rol 'cliente' —el usuario externo que
    // ve SUS rentas en un portal— necesita esa columna, pero la tabla cliente no
    // existe hasta la Fase 1. Se agrega ahi, junto con la decision de como filtrar
    // filas dentro de una misma empresa (ver 05-esquema-fase0.md 6.1).
}
