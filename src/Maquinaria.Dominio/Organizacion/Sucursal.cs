namespace Maquinaria.Dominio.Organizacion;

/// <summary>
/// Una plaza de la empresa. Agrupa ubicaciones fisicas y trabajadores.
///
/// La empresa como tal NO es una entidad de esta base: sus datos —razon social, RFC,
/// zona horaria, moneda— viven en Tenant, en la base central. Una sucursal es una
/// division interna suya.
/// </summary>
public class Sucursal
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public string? Domicilio { get; set; }

    public string? Telefono { get; set; }

    /// <summary>
    /// Cuenta contra max_sucursales del plan contratado. El limite vive en la base
    /// CENTRAL y el conteo aqui: por eso verificarlo toca dos bases.
    /// </summary>
    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public ICollection<Ubicacion> Ubicaciones { get; } = [];
}
