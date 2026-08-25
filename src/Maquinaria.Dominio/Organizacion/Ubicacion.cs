namespace Maquinaria.Dominio.Organizacion;

/// <summary>
/// Un sitio fisico de la empresa: una bodega, una sucursal o un patio.
///
/// UNA SOLA TABLA, no una jerarquia sucursal-patio. Ver <see cref="TipoUbicacion"/>
/// para por que se corrigio.
///
/// Es donde esta el equipo CUANDO NO ESTA RENTADO. Mientras esta rentado, donde esta
/// trabajando lo dice la obra de su renta. Las dos juntas responden la pregunta que el
/// documento pone como central: "donde se encuentra cada equipo".
/// </summary>
public class Ubicacion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public TipoUbicacion Tipo { get; set; }

    public string? Domicilio { get; set; }

    public string? Telefono { get; set; }

    /// <summary>Coordenadas, para el dia que la logistica calcule rutas.</summary>
    public decimal? Latitud { get; set; }

    public decimal? Longitud { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    /// <summary>
    /// Si aqui se puede resguardar equipo: bodega o patio.
    ///
    /// EXISTE TAMBIEN EN LA BASE como columna generada —GENERATED ALWAYS ... STORED— a
    /// partir de tipo. Se calcula aqui para trabajar en memoria y alla para poder
    /// consultarla y para que las reglas que cruzan tablas la usen.
    ///
    /// Generada y no capturada: asi es IMPOSIBLE escribir una fila incoherente, como una
    /// bodega que cotiza. Con una bandera normal, mantener las dos en sincronia seria
    /// trabajo de la aplicacion, y tarde o temprano una se queda atras.
    ///
    /// Del lado de C# es una propiedad derivada y EF la ignora: la escribe Postgres.
    /// </summary>
    public bool AlmacenaEquipo => Tipo is TipoUbicacion.Bodega or TipoUbicacion.Patio;

    /// <summary>
    /// Si desde aqui se administra y se cotiza: sucursal o patio. Nunca una bodega.
    ///
    /// Misma forma que <see cref="AlmacenaEquipo"/>: columna generada en la base,
    /// propiedad derivada aqui.
    /// </summary>
    public bool EsAdministrativa => Tipo is TipoUbicacion.Sucursal or TipoUbicacion.Patio;
}
