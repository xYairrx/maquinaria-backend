namespace Maquinaria.Dominio.Organizacion;

/// <summary>
/// El puesto de un trabajador: operador, mecanico, chofer, vendedor, almacenista.
///
/// Catalogo y no enum porque cada empresa nombra sus puestos distinto, y porque en la
/// Fase 2 la logistica va a preguntar "quienes pueden ser chofer de un flete" — y eso
/// se responde por puesto, no por rol del sistema.
///
/// NO ES LO MISMO QUE UN ROL. El rol dice que puede hacer alguien EN EL SISTEMA; el
/// puesto dice que hace EN LA EMPRESA. Un operador de patio tiene puesto y puede no
/// tener cuenta.
/// </summary>
public class Puesto
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public ICollection<Trabajador> Trabajadores { get; } = [];
}
