using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Catalogos;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Un renglon de la cotizacion: un concepto cobrable con su cantidad y su precio.
///
/// PUEDE NO APUNTAR A NINGUNA MAQUINA, y esa flexibilidad es deliberada. Al cotizar
/// todavia no se sabe cual de las tres excavadoras va a ir; se cotiza el TIPO. Y un
/// flete no tiene equipo ni tipo: solo tarifa y precio.
///
/// De hecho aqui hubo un error mio: la primera version llevaba un CHECK que exigia
/// equipo o tipo de equipo, y eso hacia IMPOSIBLE cotizar un flete. Salio al explicarte
/// la tabla, y por eso ya no esta.
/// </summary>
public class CotizacionLinea
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CotizacionId { get; set; }

    public Cotizacion? Cotizacion { get; set; }

    /// <summary>Que se cobra. Lo unico obligatorio del renglon.</summary>
    public Guid TarifaId { get; set; }

    public Tarifa? Tarifa { get; set; }

    /// <summary>La maquina exacta, si ya se sabe.</summary>
    public Guid? EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    /// <summary>El tipo, cuando se cotiza "una retroexcavadora" sin decir cual.</summary>
    public Guid? TipoEquipoId { get; set; }

    public TipoEquipo? TipoEquipo { get; set; }

    public string? Descripcion { get; set; }

    public decimal Cantidad { get; set; } = 1;

    public decimal PrecioUnitario { get; set; }

    public decimal Importe { get; set; }

    public int Orden { get; set; }
}
