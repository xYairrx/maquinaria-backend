namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Un modulo funcional del producto. Vive en la base central y es un CATALOGO de
/// codigo: existe porque hay pantallas y endpoints que lo implementan, no porque
/// un cliente lo invente. Se siembra desde <see cref="ClavesModulo"/>.
///
/// Es la unidad con la que se arma un plan: el plan NO es un paquete de cupos,
/// es un conjunto de modulos (ver <see cref="PlanModulo"/>). Los cupos cuelgan
/// del tenant, en <see cref="TenantLimite"/>.
/// </summary>
public class Modulo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Identificador estable, en minusculas y sin acentos. UNIQUE.
    ///
    /// Tiene que coincidir con la columna 'modulo' de la tabla permiso de la base
    /// de CADA empresa, porque la autorizacion efectiva es la interseccion de los
    /// permisos del rol con los modulos que el plan incluye. Esa relacion NO puede
    /// tener una FK: son bases de datos distintas. Es una referencia blanda, y por
    /// eso hace falta una prueba en CI que verifique que todo permiso.modulo
    /// sembrado existe aqui como clave.
    /// </summary>
    public required string Clave { get; set; }

    /// <summary>
    /// El numero del modulo en la especificacion funcional: 8 es M8, logistica.
    /// UNIQUE, y separado de <see cref="Orden"/> porque el orden de presentacion
    /// es una decision comercial que puede cambiar; el numero es la referencia
    /// estable al documento de negocio y no cambia nunca.
    /// </summary>
    public short Numero { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Posicion en la que se muestra al comparar planes.</summary>
    public int Orden { get; set; }

    /// <summary>
    /// Un modulo retirado se marca inactivo, nunca se borra: hay filas de
    /// plan_modulo historicas que lo referencian. Mismo criterio que Plan.
    /// </summary>
    public bool Activo { get; set; } = true;

    public ICollection<PlanModulo> Planes { get; } = [];
}
