namespace Maquinaria.Aplicacion.Comun;

/// <summary>
/// Por que un Servicio o un Proceso rechazo lo que le pidieron.
///
/// EXISTE PARA QUE EL CONTROLADOR NO ADIVINE EL CODIGO HTTP. Sin esto, todo rechazo se
/// traduce a 400 —es lo que hacen hoy los endpoints de planes y de empresas, y ahi es
/// correcto porque todos sus rechazos son dato mal capturado—, pero la Fase 1 tiene los
/// otros dos casos de verdad: pedir una renta que no existe es 404, y chocar con el
/// EXCLUDE de ocupacion_equipo es 409. Un 400 en cualquiera de los dos miente.
///
/// SON TRES Y NO MAS, a proposito: cada valor mapea a UN codigo HTTP y a ninguno mas. En
/// cuanto haya dos razones que dan el mismo codigo, la razon deja de decidir nada.
///
/// El 401 y el 403 NO estan aqui: los resuelve la tuberia de autorizacion antes de que
/// el Proceso corra. Un Proceso que devuelve "no autorizado" es un permiso que faltaba
/// declarar con [RequierePermiso].
/// </summary>
public enum RazonRechazo : short
{
    /// <summary>400. Dato mal capturado o que no cumple una regla de forma.</summary>
    Invalido = 1,

    /// <summary>404. La fila que se pidio no existe, o esta borrada logicamente.</summary>
    NoEncontrado = 2,

    /// <summary>
    /// 409. Choca con el estado actual o con una garantia del motor: fechas traslapadas,
    /// un contrato ya autorizado, un folio repetido.
    /// </summary>
    Conflicto = 3,
}

/// <summary>
/// El desenlace de un Proceso que no devuelve nada.
///
/// PORQUE NO EXCEPCIONES: un rechazo de negocio es un desenlace previsto, no un error. Con
/// excepciones, la ruta normal del programa incluye tirar y atrapar, el mensaje al usuario
/// sale de un <c>catch</c> generico y el manejador global tiene que distinguir entre "la
/// renta se traslapa" y "se cayo la base". Aqui el tipo de retorno obliga a considerar el
/// rechazo: no se puede leer el valor sin haber mirado <c>Correcto</c>.
///
/// Es <c>readonly record struct</c> por lo mismo que <c>ResultadoPlan</c>: no se asigna en
/// el heap y la igualdad es por valor, que es lo que las pruebas comparan.
/// </summary>
public readonly record struct Resultado(bool Correcto, RazonRechazo? Razon, string? Motivo)
{
    public static Resultado Ok() => new(true, null, null);

    public static Resultado Invalido(string motivo) => new(false, RazonRechazo.Invalido, motivo);

    public static Resultado NoEncontrado(string motivo)
        => new(false, RazonRechazo.NoEncontrado, motivo);

    public static Resultado Conflicto(string motivo)
        => new(false, RazonRechazo.Conflicto, motivo);
}

/// <summary>
/// El desenlace de un Proceso que devuelve algo.
///
/// <c>Valor</c> es nulo cuando <c>Correcto</c> es falso, y no hay forma de que no lo sea:
/// las tres fabricas de rechazo no lo aceptan. Del otro lado, quien recibe un resultado
/// correcto lee <c>Valor!</c> sin culpa.
/// </summary>
public readonly record struct Resultado<T>(
    bool Correcto,
    T? Valor,
    RazonRechazo? Razon,
    string? Motivo)
{
    public static Resultado<T> Ok(T valor) => new(true, valor, null, null);

    public static Resultado<T> Invalido(string motivo)
        => new(false, default, RazonRechazo.Invalido, motivo);

    public static Resultado<T> NoEncontrado(string motivo)
        => new(false, default, RazonRechazo.NoEncontrado, motivo);

    public static Resultado<T> Conflicto(string motivo)
        => new(false, default, RazonRechazo.Conflicto, motivo);
}
