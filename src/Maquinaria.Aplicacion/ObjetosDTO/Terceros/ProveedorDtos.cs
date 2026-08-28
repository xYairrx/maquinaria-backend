namespace Maquinaria.Aplicacion.Terceros;

/// <summary>
/// Un proveedor. Vive en la orden de compra: <c>equipo</c> **no tiene <c>proveedor_id</c>** —se
/// quito el 2026-08-25— y desde un equipo el proveedor se alcanza por
/// <c>equipo → orden_compra_detalle → orden_compra → proveedor</c>. Un dato en un solo lugar.
/// </summary>
public sealed record ProveedorDto(
    Guid Id,
    string Codigo,
    string RazonSocial,
    string? NombreComercial,
    string? Rfc,
    string? Telefono,
    string? Correo,
    string? Domicilio,
    string? Contacto,
    bool Activo,
    int OrdenesCompra);

public readonly record struct AltaProveedor(
    string Codigo,
    string RazonSocial,
    string? NombreComercial,
    string? Rfc,
    string? Telefono,
    string? Correo,
    string? Domicilio,
    string? Contacto);
