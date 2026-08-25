using Maquinaria.Dominio.Organizacion;

namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Lo que se cobra en la renta ADEMAS de las maquinas: el flete, el operador, el
/// mantenimiento.
///
/// Existe porque el flete "se cotiza sobre una renta de equipo" y porque la renta "puede
/// incluir operador", y ninguno de los dos es una maquina: no caben en
/// <see cref="RentaLinea"/>, que exige equipo.
///
/// DEL OPERADOR SE GUARDA SOLO QUIEN VA Y CUANTO SE COBRA. Lo elegiste tu entre las dos
/// opciones que te plantee, y es la decision correcta para Fase 1: registrar turnos y
/// horas del operador es nomina, y la nomina no esta en este entregable.
/// </summary>
public class RentaConcepto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RentaId { get; set; }

    public Renta? Renta { get; set; }

    /// <summary>Que concepto es. Sale del catalogo de tarifas.</summary>
    public Guid TarifaId { get; set; }

    public Tarifa? Tarifa { get; set; }

    /// <summary>
    /// El operador asignado, cuando el concepto es un operador. Nulo para un flete.
    /// </summary>
    public Guid? TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public string? Descripcion { get; set; }

    public decimal Cantidad { get; set; } = 1;

    public decimal PrecioUnitario { get; set; }

    /// <summary>
    /// Lo que le cuesta a la empresa, frente a lo que se cobra. Se captura y no se
    /// calcula: es la semilla de la rentabilidad por renta, que es Fase 2.
    /// </summary>
    public decimal? Costo { get; set; }

    public decimal Importe { get; set; }

    public DateTime CreadoEn { get; set; }
}
