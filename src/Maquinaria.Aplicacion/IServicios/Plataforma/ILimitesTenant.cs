namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// Los cupos de una empresa. Base central, y solo la plataforma los toca.
///
/// SEPARADO DE ICatalogoPlanes a proposito, y no es simetria por gusto: el plan define
/// QUE MODULOS tiene una empresa y se administra una vez en el catalogo; el cupo define
/// CUANTO y se negocia empresa por empresa. Meterlos en la misma interfaz haria que
/// administrar el catalogo comercial y ajustarle el cupo a un cliente parecieran la
/// misma operacion, que es justo la confusion que el modelo evita colgando los limites
/// del tenant y no del plan.
///
/// OJO CON LO QUE ESTO NO HACE: fijar un cupo no lo APLICA. Hoy no hay ningun caso de
/// uso que lea estos valores para bloquear una operacion —esta anotado en
/// `estado-y-pendientes.md`—, asi que esta interfaz administra un dato que todavia no
/// acota nada. Se construye igual porque el dato tiene que existir y ser editable antes
/// de que haya algo que lo lea, no despues.
/// </summary>
public interface ILimitesTenant
{
    /// <summary>
    /// Los cuatro tipos activos con su valor efectivo para esta empresa.
    ///
    /// Devuelve `null` si el slug no corresponde a ninguna empresa, que es lo que el
    /// controlador convierte en 404. Una lista vacia significaria otra cosa —una empresa
    /// que existe y no tiene cupos— y eso no puede pasar: los tipos siempre son cuatro.
    /// </summary>
    Task<IReadOnlyList<LimiteDeEmpresa>?> ListarAsync(string slug, CancellationToken ct);

    /// <summary>
    /// Fija la excepcion de un cupo, creandola o pisando la que hubiera.
    ///
    /// PUT y no PATCH en el endpoint por eso mismo: el cupo de un tipo es un valor
    /// unico, asi que mandarlo es reemplazarlo, sea la primera vez o la quinta.
    /// </summary>
    Task<ResultadoLimites> FijarAsync(
        string slug, string clave, int valor, CancellationToken ct);

    /// <summary>
    /// Borra la excepcion y devuelve el cupo al valor por defecto del catalogo.
    ///
    /// Quitar la fila y no escribir el valor por defecto en ella: son dos cosas
    /// distintas. Con la fila puesta, cambiar el valor por defecto del catalogo dejaria
    /// a esta empresa anclada al numero viejo sin que nadie lo pidiera.
    ///
    /// Es idempotente: quitar un cupo que no tenia excepcion no es un error.
    /// </summary>
    Task<ResultadoLimites> QuitarAsync(string slug, string clave, CancellationToken ct);
}
