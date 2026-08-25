using Maquinaria.Dominio.Catalogos;
using Maquinaria.Dominio.Organizacion;

namespace Maquinaria.Dominio.Activos;

/// <summary>
/// Una maquina concreta. NO un modelo: dos excavadoras del mismo modelo son dos filas.
///
/// Es el centro de la Fase 1. La renta compromete equipos, la venta los saca, la compra
/// los mete, y la pregunta que el documento pone como central —donde esta cada equipo—
/// se responde con <see cref="UbicacionId"/> cuando descansa y con la renta cuando
/// trabaja.
///
/// NO LLEVA proveedor_id. Lo quitaste tu, y con razon: de quien se compro es un hecho de
/// la orden de compra, no un atributo de la maquina. Dejarlo aqui obligaria a mantenerlo
/// en dos lugares y a inventar un valor para las maquinas de carga inicial.
/// </summary>
public class Equipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>El numero economico con el que la empresa lo llama.</summary>
    public required string CodigoInterno { get; set; }

    public Guid ModeloEquipoId { get; set; }

    public ModeloEquipo? Modelo { get; set; }

    /// <summary>
    /// Repetido respecto al modelo A PROPOSITO: el tipo se consulta y se filtra en cada
    /// pantalla de disponibilidad, y llegar a el por el modelo obligaria a una union en
    /// la consulta mas caliente del sistema.
    /// </summary>
    public Guid TipoEquipoId { get; set; }

    public TipoEquipo? Tipo { get; set; }

    /// <summary>
    /// Donde se resguarda cuando no esta rentado. NULO mientras esta en obra.
    ///
    /// Solo puede apuntar a una bodega o un patio, nunca a una sucursal. No lo garantiza
    /// esta propiedad sino un disparador en la base, porque la regla cruza dos tablas y
    /// una restriccion CHECK no puede mirar otra fila.
    /// </summary>
    public Guid? UbicacionId { get; set; }

    public Ubicacion? Ubicacion { get; set; }

    public string? NumeroSerie { get; set; }

    public int? Anio { get; set; }

    public EstadoEquipo Estado { get; set; } = EstadoEquipo.Disponible;

    public PropositoEquipo Proposito { get; set; } = PropositoEquipo.Renta;

    public OrigenEquipo Origen { get; set; } = OrigenEquipo.Compra;

    public DateOnly? FechaAdquisicion { get; set; }

    public decimal? CostoAdquisicion { get; set; }

    public decimal? ValorActual { get; set; }

    /// <summary>Horas de motor. Es la lectura con la que se cobran las rentas por hora.</summary>
    public decimal? Horometro { get; set; }

    public decimal? Kilometraje { get; set; }

    /// <summary>
    /// Para la etiqueta QR pegada en la maquina. Unico, y es un token y no el id: un id
    /// impreso en una calcomania es un id que cualquiera puede leer y enumerar.
    /// </summary>
    public string? TokenQr { get; set; }

    public string? Notas { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }

    /// <summary>
    /// BAJA LOGICA, nunca fisica. Una maquina borrada de verdad se llevaria por delante
    /// el historial de rentas que la referencia.
    /// </summary>
    public DateTime? EliminadoEn { get; set; }

    public ICollection<EquipoArchivo> Archivos { get; set; } = [];

    public ICollection<EquipoTarifa> Tarifas { get; set; } = [];
}
