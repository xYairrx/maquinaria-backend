namespace Maquinaria.Dominio.Terceros;

/// <summary>
/// A quien se le renta y, si llega el caso, a quien se le vende.
///
/// UNA SOLA TABLA. Al principio propuse tres —cliente, contacto_cliente,
/// domicilio_cliente— y lo corregiste: "cliente, contacto cliente y domicilio agregalo
/// en la misma tabla cliente". Tenias razon para la Fase 1: en la practica cada cliente
/// tiene un contacto y una direccion fiscal, y tres tablas obligaban a dos uniones y a
/// un formulario en tres pasos para capturar lo que cabe en uno.
///
/// El dia que un cliente necesite varios contactos, eso es una tabla nueva que cuelga de
/// aqui, no un rediseno de esta.
///
/// NO SE CONFUNDE CON PROVEEDOR aunque se parezcan: a un cliente se le cobra y tiene
/// credito y deposito; a un proveedor se le paga. Fusionarlos en un "tercero" haria que
/// la mitad de las columnas sobraran siempre.
/// </summary>
public class Cliente
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Codigo { get; set; }

    public required string RazonSocial { get; set; }

    public string? NombreComercial { get; set; }

    public string? Rfc { get; set; }

    public string? Telefono { get; set; }

    public string? Correo { get; set; }

    public string? ContactoNombre { get; set; }

    public string? ContactoPuesto { get; set; }

    public string? ContactoTelefono { get; set; }

    public string? ContactoCorreo { get; set; }

    public string? Calle { get; set; }

    public string? Colonia { get; set; }

    public string? Municipio { get; set; }

    /// <summary>
    /// El estado de la republica. Se llama asi para no chocar con
    /// <see cref="Estado"/>, que es la situacion del cliente.
    /// </summary>
    public string? EstadoProv { get; set; }

    public string? CodigoPostal { get; set; }

    public string Pais { get; set; } = "MX";

    public decimal? Latitud { get; set; }

    public decimal? Longitud { get; set; }

    /// <summary>
    /// Cuanto se le puede fiar. En Fase 1 SE CAPTURA Y NO SE CALCULA NADA con el: lo
    /// acordamos asi —"por lo pronto para fase 1 no haremos calculos"—. Esta aqui para
    /// que el dato exista desde el principio y la Fase 2 no tenga que pedirlo de nuevo.
    /// </summary>
    public decimal LimiteCredito { get; set; }

    public int DiasCredito { get; set; }

    public decimal DepositoRequerido { get; set; }

    public string? Condiciones { get; set; }

    public EstadoCliente Estado { get; set; } = EstadoCliente.Activo;

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }
}
