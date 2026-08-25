namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Un concepto cobrable del catalogo: renta diaria, mantenimiento, flete, operador,
/// maniobras.
///
/// DECIDIDO EL 2026-08-24: una tarifa NO es solo el precio de rentar un equipo por
/// periodo. Es un concepto que se cobra, y una renta o una venta puede arrastrar VARIOS.
///
/// Eso unifica cosas que si no tendrian tabla propia cada una:
///
/// - el flete se cotiza sobre la renta   -> una linea con tarifa de flete
/// - la renta puede incluir operador     -> una linea con tarifa de operador, mas el
///                                          trabajador que va
/// - el mantenimiento se cobra           -> una linea con tarifa de mantenimiento
///
/// El PRECIO no vive aqui: vive en EquipoTarifa, porque depende del equipo y cambia con
/// el tiempo. Aqui vive el concepto.
/// </summary>
public class Tarifa
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Por que se multiplica el precio. Ver <see cref="UnidadTarifa"/>.</summary>
    public UnidadTarifa Unidad { get; set; }

    /// <summary>Si puede aparecer en una renta.</summary>
    public bool AplicaRenta { get; set; } = true;

    /// <summary>
    /// Si puede aparecer en una venta de equipo.
    ///
    /// Son dos banderas y no un enum de ambito porque hay tarifas que aplican a las dos
    /// —maniobras, flete— y un enum obligaria a un tercer valor "Ambas" que despues hay
    /// que recordar incluir en cada consulta.
    /// </summary>
    public bool AplicaVenta { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }
}
