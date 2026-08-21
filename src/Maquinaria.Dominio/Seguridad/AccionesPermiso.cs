namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Las seis acciones que puede autorizar un <see cref="Permiso"/>.
///
/// Constantes y no enum porque la clave del permiso es texto —'equipos.editar'— y
/// se compone concatenando modulo y accion. Un enum obligaria a traducirlo en cada
/// uso, y el valor que viaja en el JWT y se compara es la cadena.
/// </summary>
public static class AccionesPermiso
{
    public const string Consultar = "consultar";
    public const string Crear = "crear";
    public const string Editar = "editar";
    public const string Eliminar = "eliminar";
    public const string Autorizar = "autorizar";
    public const string Exportar = "exportar";

    public static readonly string[] Todas =
    [
        Consultar,
        Crear,
        Editar,
        Eliminar,
        Autorizar,
        Exportar,
    ];
}
