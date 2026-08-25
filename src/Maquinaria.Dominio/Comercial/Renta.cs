using Maquinaria.Dominio.Organizacion;
using Maquinaria.Dominio.Terceros;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// El contrato economico: a quien, que equipos, desde cuando, hasta cuando y donde.
///
/// DONDE SE TRABAJA VA AQUI DENTRO, no en una tabla obra. Lo decidiste asi: "quita la
/// tabla obra, se sustituye unicamente por una descripcion en renta, mas bien guarda la
/// direccion de donde se rentara". De ahi el grupo de campos lugar_*.
///
/// Es la respuesta a la otra mitad de "donde esta cada equipo": mientras la renta esta
/// activa, el equipo esta en este lugar y no en su ubicacion.
///
/// EL CALENDARIO NO SE CONTROLA AQUI sino en <c>ocupacion_equipo</c>. Esta tabla dice el
/// periodo comercial; esa dice, equipo por equipo, que periodos estan tomados, y es la
/// que impide rentar dos veces las mismas fechas.
/// </summary>
public class Renta
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Folio { get; set; }

    public Guid ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    /// <summary>De donde salio, si salio de una cotizacion. Nulo si se levanto directo.</summary>
    public Guid? CotizacionId { get; set; }

    public Cotizacion? Cotizacion { get; set; }

    /// <summary>Quien la levanto.</summary>
    public Guid TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public DateTime Inicio { get; set; }

    public DateTime Fin { get; set; }

    public EstadoRenta Estado { get; set; } = EstadoRenta.Borrador;

    /// <summary>
    /// Donde se va a trabajar, en palabras: "Obra Torre Norte, km 4 carretera federal".
    /// Obligatorio y no vacio: una renta sin lugar es una maquina que nadie sabe donde
    /// esta.
    /// </summary>
    public required string LugarDescripcion { get; set; }

    public string? LugarCalle { get; set; }

    public string? LugarColonia { get; set; }

    public string? LugarMunicipio { get; set; }

    public string? LugarEstadoProv { get; set; }

    public string? LugarCodigoPostal { get; set; }

    public decimal? LugarLatitud { get; set; }

    public decimal? LugarLongitud { get; set; }

    /// <summary>A quien buscar en la obra. No es el contacto del cliente.</summary>
    public string? LugarContacto { get; set; }

    public string? LugarTelefono { get; set; }

    public decimal Deposito { get; set; }

    public decimal Anticipo { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Descuento { get; set; }

    public decimal Impuestos { get; set; }

    public decimal Total { get; set; }

    /// <summary>Se captura. En Fase 1 nada lo calcula.</summary>
    public decimal Saldo { get; set; }

    public string? Notas { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }

    /// <summary>Las maquinas rentadas y su precio.</summary>
    public ICollection<RentaLinea> Lineas { get; set; } = [];

    /// <summary>Lo demas que se cobra: fletes, operador, mantenimiento.</summary>
    public ICollection<RentaConcepto> Conceptos { get; set; } = [];

    public ICollection<ExtensionRenta> Extensiones { get; set; } = [];
}
