namespace Maquinaria.Dominio.Catalogos;

/// <summary>Fabricante: Caterpillar, Komatsu, JCB.</summary>
public class Marca
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public ICollection<ModeloEquipo> Modelos { get; } = [];
}
