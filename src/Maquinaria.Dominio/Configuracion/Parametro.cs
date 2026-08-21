namespace Maquinaria.Dominio.Configuracion;

/// <summary>
/// Configuracion de la empresa, en formato clave/valor.
///
/// Es el otro caso —junto con tipo_limite— en el que clave/valor se justifica:
/// agregar un parametro no requiere migracion ni desplegar. En tablas de negocio
/// seria un antipatron.
///
/// SE AUDITA. Un cambio de parametro puede alterar como se calcula un cobro, asi
/// que entra en la lista opt-in del interceptor.
/// </summary>
public class Parametro
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>UNIQUE.</summary>
    public required string Clave { get; set; }

    /// <summary>
    /// Siempre texto. Como interpretarlo lo dice <see cref="Tipo"/>, y la capa de
    /// aplicacion es la que convierte.
    /// </summary>
    public required string Valor { get; set; }

    public TipoParametro Tipo { get; set; }
}
