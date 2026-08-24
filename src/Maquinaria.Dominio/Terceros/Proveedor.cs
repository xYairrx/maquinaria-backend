namespace Maquinaria.Dominio.Terceros;

/// <summary>
/// Quien nos vende o nos presta servicios.
///
/// Se crea ya en la Fase 1, minimo, por dos razones: el equipo registra a quien se le
/// compro, y la subrenta del M30 necesita el proveedor del que viene la maquina. El
/// M18 completo —tarifas, historial, compras, pagos— es Fase 3 y solo le agregara
/// columnas y tablas alrededor, sin cambiar esta forma.
/// </summary>
public class Proveedor
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Codigo { get; set; }

    public required string RazonSocial { get; set; }

    public string? NombreComercial { get; set; }

    public string? Rfc { get; set; }

    public string? Telefono { get; set; }

    public string? Correo { get; set; }

    public string? Domicilio { get; set; }

    public string? Contacto { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }
}
