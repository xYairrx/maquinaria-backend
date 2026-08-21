namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Slugs que NO puede tomar ninguna empresa.
///
/// No es cosmetica: <see cref="Tenant.NombreBd"/> se deriva del slug, asi que un
/// tenant con slug 'plantilla' generaria nombre_bd = maquinaria_plantilla y CHOCARIA
/// con la base de tiempo de diseno. Igual 'central'. El resto se reserva porque son
/// subdominios que vamos a querer para nosotros.
///
/// Este hueco no estaba en ningun documento de diseno: se detecto al cerrar la
/// decision de los nombres de las bases.
/// </summary>
public static class SlugsReservados
{
    private static readonly HashSet<string> Reservados = new(StringComparer.Ordinal)
    {
        // Chocarian con una base existente.
        "central",
        "plantilla",

        // Bases que Postgres y Neon ya usan.
        "postgres",
        "neondb",
        "template0",
        "template1",

        // Subdominios y rutas que queremos para la plataforma.
        "admin",
        "api",
        "app",
        "www",
        "soporte",
        "status",
    };

    public static bool EstaReservado(string slug)
        => Reservados.Contains(slug.Trim().ToLowerInvariant());

    public static IReadOnlyCollection<string> Todos => Reservados;
}
