namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Que puede hacer un rol. Llave compuesta, sin uuid de sustitucion: nadie
/// referencia una fila de esta tabla.
///
/// SE SIEMBRA VACIA. Los nueve roles se crean sin ningun permiso: el reparto lo
/// define el administrador de cada empresa, porque en una empresa ventas autoriza
/// y en otra solo cotiza. El arranque no depende de esta tabla porque
/// 'administrador' trae <see cref="Rol.AccesoTotal"/>.
///
/// Es una de las dos tablas que la auditoria tiene que poder registrar —quien le
/// dio que poder a quien— y por eso auditoria.entidad_id es text: aqui la llave
/// son dos uuid, no uno.
/// </summary>
public class RolPermiso
{
    public Guid RolId { get; set; }

    public Guid PermisoId { get; set; }

    public Rol? Rol { get; set; }

    public Permiso? Permiso { get; set; }
}
