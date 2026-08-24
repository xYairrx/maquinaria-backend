namespace Maquinaria.Dominio.Catalogos;

/// <summary>
/// El primer nivel de clasificacion del parque: maquinaria pesada, maquinaria ligera,
/// equipo de construccion, herramienta, vehiculos, generadores.
///
/// Es catalogo POR EMPRESA y no global. Se penso hacerlo global —las categorias de
/// maquinaria son casi universales— y se descarto: en cuanto una empresa quiera
/// renombrar "Maquinaria ligera" o partirla en dos, un catalogo global obliga a
/// desplegar. El costo de que cada empresa tenga las suyas es sembrarlas al
/// aprovisionar, una sola vez.
/// </summary>
public class CategoriaEquipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Corto y estable, para reportes y para el codigo interno del equipo.</summary>
    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>
    /// Una categoria retirada se marca inactiva, nunca se borra: hay equipos historicos
    /// que la referencian. Mismo criterio que Plan en la base central.
    /// </summary>
    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public ICollection<TipoEquipo> Tipos { get; } = [];
}
