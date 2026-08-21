namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Los codigos de los nueve roles del modulo 25, que se siembran en cada base de
/// empresa.
///
/// Son SEMILLA, no un enum fijo: cada empresa los renombra y ajusta —en una,
/// ventas cotiza y autoriza; en otra, solo cotiza—. Por eso el codigo es lo unico
/// estable, y por eso la verificacion de acceso total NO pregunta por
/// <see cref="Administrador"/> sino por la columna acceso_total: un rename
/// legitimo apagaria el bypass, y alguien podria crear un rol llamado
/// 'administrador' y ganarse el poder sin que nadie se lo conceda.
/// </summary>
public static class ClavesRol
{
    /// <summary>
    /// El unico con acceso_total. No se puede editar ni borrar, y no aparece en la
    /// interfaz de asignaciones: se otorga solo al aprovisionar la empresa.
    /// </summary>
    public const string Administrador = "administrador";

    public const string Direccion = "direccion";
    public const string Ventas = "ventas";
    public const string Rentas = "rentas";
    public const string Logistica = "logistica";
    public const string Taller = "taller";
    public const string Operador = "operador";
    public const string Cobranza = "cobranza";
    public const string Cliente = "cliente";

    public static readonly string[] Todos =
    [
        Administrador,
        Direccion,
        Ventas,
        Rentas,
        Logistica,
        Taller,
        Operador,
        Cobranza,
        Cliente,
    ];
}
