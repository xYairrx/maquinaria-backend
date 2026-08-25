namespace Maquinaria.Dominio.Organizacion;

/// <summary>
/// Una persona que trabaja en la empresa.
///
/// NO ESTA EN LA ESPECIFICACION FUNCIONAL: se agrego el 2026-08-21 a peticion del
/// negocio. El documento solo contempla "usuarios" —cuentas de acceso— y "operador"
/// dentro del modulo de fletes.
///
/// TRABAJADOR Y USUARIO SON COSAS DISTINTAS, y confundirlos es el error a evitar:
///
/// - Un trabajador es una PERSONA con un puesto. El operador del patio o el mecanico
///   pueden no tener cuenta en el sistema y aun asi hay que registrarlos, asignarles
///   equipo y saber en que sucursal estan.
/// - Un usuario es una CUENTA con roles y permisos. El administrador de la empresa
///   podria no ser trabajador, y en la Fase 1 del portal el rol 'cliente' sera un
///   usuario externo que no trabaja aqui.
///
/// La liga es opcional en los dos sentidos, y por eso <see cref="UsuarioId"/> es
/// nullable y unico. Se puso la llave foranea DE ESTE LADO a proposito: asi la tabla
/// usuario —que ya esta migrada y es de la Fase 0— no se toca.
/// </summary>
public class Trabajador
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Numero de empleado. UNIQUE.</summary>
    public required string NumeroEmpleado { get; set; }

    public required string Nombre { get; set; }

    public string? Apellidos { get; set; }

    public Guid PuestoId { get; set; }

    /// <summary>
    /// A que sitio esta adscrito. Nullable: puede no tener uno fijo.
    ///
    /// Deberia ser una ubicacion ADMINISTRATIVA —sucursal o patio—, no una bodega. Eso
    /// cruza dos tablas y lo hace cumplir el dominio, no un CHECK.
    /// </summary>
    public Guid? UbicacionId { get; set; }

    /// <summary>
    /// Su cuenta en el sistema, si tiene. NULL significa que trabaja aqui pero no entra
    /// al sistema, que es el caso de la mayoria del personal de patio.
    /// </summary>
    public Guid? UsuarioId { get; set; }

    public string? Telefono { get; set; }

    public string? Correo { get; set; }

    public EstadoTrabajador Estado { get; set; } = EstadoTrabajador.Activo;

    public DateOnly? FechaIngreso { get; set; }

    public DateOnly? FechaBaja { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }

    public Puesto? Puesto { get; set; }

    public Ubicacion? Ubicacion { get; set; }
}
