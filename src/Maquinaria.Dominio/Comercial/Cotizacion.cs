using Maquinaria.Dominio.Organizacion;
using Maquinaria.Dominio.Terceros;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Lo que se le propone a un cliente antes de que haya renta.
///
/// SE EMITE DESDE UNA SUCURSAL O UN PATIO, nunca desde una bodega. Es tu regla:
/// "sucursal es para administracion y cotizar a los clientes sobre rentas". La garantiza
/// un disparador contra la columna generada <c>es_administrativa</c> de ubicacion.
///
/// NO ESTA OBLIGADA A EXISTIR: <see cref="Renta.CotizacionId"/> es nulo cuando la renta
/// se levanta directo. Muchas rentas de repeticion no pasan por cotizar, y exigirla
/// obligaria a inventar cotizaciones falsas.
///
/// LOS MONTOS SE CAPTURAN, NO SE CALCULAN. Acordamos que la Fase 1 no hace cuentas.
/// </summary>
public class Cotizacion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Folio { get; set; }

    public Guid ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    /// <summary>Desde donde se cotiza. Sucursal o patio.</summary>
    public Guid UbicacionId { get; set; }

    public Ubicacion? Ubicacion { get; set; }

    /// <summary>Quien la levanto.</summary>
    public Guid TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public DateOnly Fecha { get; set; }

    public DateOnly? VigenciaHasta { get; set; }

    public EstadoCotizacion Estado { get; set; } = EstadoCotizacion.Borrador;

    public decimal Subtotal { get; set; }

    public decimal Descuento { get; set; }

    public decimal Impuestos { get; set; }

    public decimal Total { get; set; }

    public string? Notas { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }

    public ICollection<CotizacionLinea> Lineas { get; set; } = [];
}
