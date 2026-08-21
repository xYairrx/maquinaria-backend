namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Catalogo de los tipos de limite que el sistema sabe aplicar. Base central.
///
/// Es el espejo en base de datos de <see cref="ClavesLimite"/>, y existe por la
/// misma razon que la tabla permiso: la clave deja de ser texto libre y pasa a
/// tener integridad referencial, asi que un "max_equipoz" ya no se puede escribir.
///
/// Ojo con lo que esta tabla NO da: que el tipo de limite sea una fila no hace que
/// un limite nuevo funcione sin desplegar. Un limite solo acota cuando hay codigo
/// que lo lee y bloquea la operacion. El catalogo solo le pone nombre.
/// </summary>
public class TipoLimite
{
    /// <summary>
    /// Valor que significa "sin limite". Es el unico negativo que admite el CHECK
    /// de valor_defecto y el de tenant_limite.valor.
    /// </summary>
    public const int Ilimitado = -1;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Una de las constantes de <see cref="ClavesLimite"/>. UNIQUE.</summary>
    public required string Clave { get; set; }

    /// <summary>Nombre para mostrar al comparar planes: "Equipos".</summary>
    public required string Nombre { get; set; }

    public required string Descripcion { get; set; }

    /// <summary>Unidad en la que se cuenta: equipos, usuarios, GB.</summary>
    public required string Unidad { get; set; }

    /// <summary>
    /// Lo que aplica cuando el tenant no declara este limite. Arranca en
    /// <see cref="Ilimitado"/> a proposito: asi dar de alta una empresa no tiene
    /// que insertar ni una fila de tenant_limite, y nadie queda limitado por
    /// omision.
    ///
    /// NO lleva DEFAULT en la base, y no es un olvido. Con un DEFAULT -1, EF Core
    /// omitiria la columna al insertar un tipo con ValorDefecto = 0 —porque 0 es
    /// el valor sentinel de int— y un limite que quiso decir "cero permitido"
    /// se guardaria como ilimitado. Es la misma trampa que llevo a dejar
    /// plan.activo sin DEFAULT.
    /// </summary>
    public int ValorDefecto { get; set; } = Ilimitado;

    /// <summary>Posicion en la que se muestra al comparar planes.</summary>
    public int Orden { get; set; }

    /// <summary>
    /// Un tipo retirado se marca inactivo, nunca se borra: la FK de tenant_limite
    /// es RESTRICT precisamente para impedirlo.
    /// </summary>
    public bool Activo { get; set; } = true;
}
