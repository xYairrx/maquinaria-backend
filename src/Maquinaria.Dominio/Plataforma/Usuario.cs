namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Superadministrador: nosotros, los duenos del SaaS.
///
/// Vive SOLO en la base central y no existe en ninguna base de empresa, asi que
/// un error de permisos dentro de una empresa no puede alcanzar la plataforma.
/// Por lo mismo no tiene ninguna relacion con las otras entidades: es una isla
/// a proposito.
///
/// HOMONIMA de la entidad Usuario de la base de empresa, y eso es deliberado: son
/// la misma idea en dos mundos separados fisicamente. El namespace las distingue,
/// y confundirlas no compila — cada una existe solo en su propio DbContext, asi
/// que pedirle un DbSet&lt;Plataforma.Usuario&gt; a ContextoEmpresa es un error de
/// compilacion, no un bug en produccion.
/// </summary>
public class Usuario
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Normalizado a minusculas al escribir, en la capa de aplicacion. UNIQUE.</summary>
    public required string Correo { get; set; }

    /// <summary>
    /// Required aqui, y NULLABLE en la tabla usuario de la base de empresa. No es
    /// inconsistencia: alla los usuarios se crean por invitacion, asi que la fila
    /// existe sin hash mientras la persona no define su contrasena. A un
    /// superadministrador lo creamos nosotros con contrasena desde el principio.
    /// </summary>
    public required string HashContrasena { get; set; }

    public required string Nombre { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime? UltimoAccesoEn { get; set; }

    public DateTime CreadoEn { get; set; }
}
