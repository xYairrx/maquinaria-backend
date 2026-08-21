namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Que roles tiene un usuario. N:N a proposito: en una empresa chica la misma
/// persona es ventas y cobranza.
///
/// Llave compuesta, sin uuid de sustitucion. Igual que <see cref="RolPermiso"/>,
/// es material de auditoria de primera linea: asignar el rol 'administrador' a
/// alguien es la operacion mas privilegiada que existe dentro de una empresa.
/// </summary>
public class UsuarioRol
{
    public Guid UsuarioId { get; set; }

    public Guid RolId { get; set; }

    public Usuario? Usuario { get; set; }

    public Rol? Rol { get; set; }
}
