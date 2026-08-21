namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Un permiso concreto: una accion sobre un modulo. Catalogo de CODIGO, sembrado
/// identico en cada base de empresa durante el aprovisionamiento.
///
/// Los permisos son parte del codigo: existen porque hay un endpoint que los
/// verifica. Ningun cliente inventa permisos. Cada migracion que agrega un modulo
/// agrega tambien sus permisos.
///
/// LA AUTORIZACION EFECTIVA ES UNA INTERSECCION, no una lectura:
///
///     permisos del rol  n  modulos del plan del tenant
///
/// Un usuario con 'logistica.crear' en una empresa cuyo plan no incluye logistica
/// NO puede crear un flete. Y el catalogo modulo vive en la base CENTRAL, asi
/// que la relacion entre esta columna y modulo.clave NO PUEDE TENER FK:
/// son bases distintas. Es una referencia blanda y hace falta una prueba en CI que
/// verifique que todo Modulo sembrado aqui existe alla como clave.
/// </summary>
public class Permiso
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Modulo punto accion: 'equipos.editar'. UNIQUE.</summary>
    public required string Clave { get; set; }

    /// <summary>
    /// Una clave de ClavesModulo, de la base central. Sin FK posible: la tabla
    /// modulo esta en otra base de datos.
    /// </summary>
    public required string Modulo { get; set; }

    /// <summary>Una de las constantes de <see cref="AccionesPermiso"/>.</summary>
    public required string Accion { get; set; }

    public required string Descripcion { get; set; }

    public ICollection<RolPermiso> Roles { get; } = [];
}
