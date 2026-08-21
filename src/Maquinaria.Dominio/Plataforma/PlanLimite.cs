namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Un limite de un plan, en formato clave/valor.
///
/// Se eligio clave/valor en lugar de columnas para que agregar un limite nuevo no
/// requiera migracion ni desplegar. El precio es perder la verificacion de tipos:
/// nada impide escribir "max_equipoz" y que el limite no se aplique nunca, en
/// silencio. Eso se compensa con <see cref="ClavesLimite"/>.
///
/// Este intercambio solo vale la pena en tablas de configuracion. En tablas de
/// negocio, clave/valor es un antipatron.
/// </summary>
public class PlanLimite
{
    /// <summary>Valor que significa "sin limite".</summary>
    public const int Ilimitado = -1;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PlanId { get; set; }

    /// <summary>Una de las constantes de <see cref="ClavesLimite"/>. UNIQUE junto con PlanId.</summary>
    public required string Clave { get; set; }

    /// <summary><see cref="Ilimitado"/> o un entero mayor o igual a cero.</summary>
    public int Valor { get; set; }

    /// <summary>
    /// Navegacion de vuelta. Nullable porque solo esta poblada si la consulta la
    /// pidio: si fuera "required" mentiria sobre lo que hay en memoria.
    /// </summary>
    public Plan? Plan { get; set; }
}
