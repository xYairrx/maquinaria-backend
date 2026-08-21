namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// El unico lugar donde se escriben las claves de <see cref="PlanLimite"/>.
///
/// No es opcional: es lo que recupera la verificacion de tipos que el formato
/// clave/valor pierde. Sin esta clase, un "max_equipoz" mal escrito compila,
/// se guarda, y el limite simplemente nunca se aplica, sin ningun error.
/// </summary>
public static class ClavesLimite
{
    public const string MaxEquipos = "max_equipos";
    public const string MaxUsuarios = "max_usuarios";
    public const string MaxSucursales = "max_sucursales";
    public const string MaxAlmacenamientoGb = "max_almacenamiento_gb";

    /// <summary>
    /// Todas las claves validas. Sirve para validar las semillas de planes y para
    /// detectar filas con claves huerfanas tras un cambio de nombre.
    /// </summary>
    public static readonly string[] Todas =
    [
        MaxEquipos,
        MaxUsuarios,
        MaxSucursales,
        MaxAlmacenamientoGb,
    ];
}
