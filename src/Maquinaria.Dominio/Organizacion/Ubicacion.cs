namespace Maquinaria.Dominio.Organizacion;

/// <summary>
/// Un lugar fisico concreto dentro de una sucursal: un patio, una bodega, un taller.
///
/// Es donde esta el equipo CUANDO NO ESTA RENTADO. Mientras esta rentado, donde esta
/// trabajando lo dice la obra de su renta, no esta tabla. Las dos juntas responden la
/// pregunta que el documento pone como central: "donde se encuentra cada equipo".
/// </summary>
public class Ubicacion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SucursalId { get; set; }

    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public TipoUbicacion Tipo { get; set; } = TipoUbicacion.Patio;

    /// <summary>Coordenadas, para el dia que la logistica calcule rutas.</summary>
    public decimal? Latitud { get; set; }

    public decimal? Longitud { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public Sucursal? Sucursal { get; set; }
}
