using Maquinaria.Dominio.Comercial;
using Maquinaria.Dominio.Terceros;

namespace Maquinaria.Dominio.Activos;

/// <summary>
/// Cuanto cuesta un concepto cobrable PARA ESTA MAQUINA.
///
/// "Las tarifas son por equipo", dijiste, y de ahi esta tabla: <c>tarifa</c> es el
/// catalogo de conceptos —renta por dia, mantenimiento, flete— y aqui vive el precio de
/// cada concepto para cada maquina.
///
/// DOS COSAS QUE VAN MAS ALLA DE LO QUE PEDISTE, y que conviene que sepas para poder
/// quitarlas si no las quieres:
///
/// - <see cref="ClienteId"/> permite un precio especial para un cliente. En nulo, es el
///   precio de lista.
/// - la vigencia permite subir precios sin perder el historico de lo ya cotizado.
///
/// Las dos las protege una restriccion EXCLUDE en la base: no puede haber dos precios
/// del mismo concepto, para la misma maquina y el mismo cliente, con periodos que se
/// traslapen. Sin ella, "cual es el precio hoy" tendria dos respuestas.
/// </summary>
public class EquipoTarifa
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    public Guid TarifaId { get; set; }

    public Tarifa? Tarifa { get; set; }

    /// <summary>Nulo = precio de lista. Con valor = precio negociado con ese cliente.</summary>
    public Guid? ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public decimal Precio { get; set; }

    public string Moneda { get; set; } = "MXN";

    public DateTime VigenciaDesde { get; set; }

    /// <summary>Nulo = sigue vigente.</summary>
    public DateTime? VigenciaHasta { get; set; }

    public DateTime CreadoEn { get; set; }
}
