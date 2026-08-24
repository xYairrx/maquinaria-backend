namespace Maquinaria.Dominio.Catalogos;

/// <summary>
/// Un modelo concreto de una marca: la 320D de Caterpillar.
///
/// Es entidad y no un texto libre en Equipo porque de el cuelgan datos que se repiten
/// en cada unidad —capacidades, peso, manual— y porque el dia que se pregunte "cuanto
/// rinde una 320D contra una PC200" hay que poder agrupar sin depender de que nadie
/// escribiera el nombre distinto.
/// </summary>
public class ModeloEquipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MarcaId { get; set; }

    /// <summary>El tipo por defecto de este modelo. El equipo puede afinarlo.</summary>
    public Guid? TipoEquipoId { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>
    /// Horas de servicio recomendadas por el fabricante entre mantenimientos. Nullable
    /// porque no siempre se conoce. Lo usara ProximoServicio en la Fase 3.
    /// </summary>
    public int? HorasEntreServicios { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public Marca? Marca { get; set; }

    public TipoEquipo? TipoEquipo { get; set; }
}
