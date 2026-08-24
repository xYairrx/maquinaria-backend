namespace Maquinaria.Dominio.Catalogos;

/// <summary>
/// El segundo nivel: dentro de "Maquinaria pesada", una excavadora es un tipo distinto
/// de un compactador.
///
/// Cuelga de <see cref="CategoriaEquipo"/> y no es una lista plana, porque el documento
/// pide categoria Y tipo como datos separados de identificacion, y porque la plantilla
/// de inspeccion de la Fase 2 se configura por categoria: el checklist de una excavadora
/// no es el de un generador.
/// </summary>
public class TipoEquipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CategoriaEquipoId { get; set; }

    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public CategoriaEquipo? Categoria { get; set; }
}
