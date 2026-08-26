using Maquinaria.Dominio.Terceros;

namespace Maquinaria.Aplicacion.Terceros;

/// <summary>
/// Un cliente, con su contacto y su domicilio DENTRO.
///
/// Se quitaron <c>contacto_cliente</c> y <c>domicilio_cliente</c> el 2026-08-25 y sus campos
/// viven aqui. El precio, dicho en voz alta: **un cliente tiene un solo contacto y un solo
/// domicilio**. Si manana hace falta el domicilio fiscal aparte del de entrega, o dos
/// contactos —cobranza y operacion—, hay que volver a sacar la tabla y migrar los datos. Fue
/// una decision del negocio, no un descuido.
/// </summary>
public sealed record ClienteDto(
    Guid Id,
    string Codigo,
    string RazonSocial,
    string? NombreComercial,
    string? Rfc,
    string? Telefono,
    string? Correo,
    ContactoCliente Contacto,
    DomicilioCliente Domicilio,
    decimal LimiteCredito,
    int DiasCredito,
    decimal DepositoRequerido,
    string? Condiciones,
    EstadoCliente Estado,
    int Rentas);

/// <summary>
/// El contacto, agrupado en un objeto propio del DTO aunque en la tabla sean cuatro columnas
/// planas. La pantalla lo pinta como un bloque y el JSON lo refleja; la base no cambia.
/// </summary>
public readonly record struct ContactoCliente(
    string? Nombre,
    string? Puesto,
    string? Telefono,
    string? Correo);

public readonly record struct DomicilioCliente(
    string? Calle,
    string? Colonia,
    string? Municipio,
    string? EstadoProv,
    string? CodigoPostal,
    string Pais,
    decimal? Latitud,
    decimal? Longitud);

public readonly record struct AltaCliente(
    string Codigo,
    string RazonSocial,
    string? NombreComercial,
    string? Rfc,
    string? Telefono,
    string? Correo,
    ContactoCliente Contacto,
    DomicilioCliente Domicilio,
    decimal LimiteCredito,
    int DiasCredito,
    decimal DepositoRequerido,
    string? Condiciones);

public sealed record FiltroClientes : Comun.Filtro
{
    public EstadoCliente? Estado { get; init; }
}
