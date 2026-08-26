namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// Un plan como lo ve el panel de plataforma.
/// </summary>
/// <param name="Modulos">
/// LAS CLAVES, no los nombres. El plan ES su conjunto de modulos —ver
/// <see cref="Dominio.Plataforma.PlanModulo"/>— asi que esto no es un adorno del DTO:
/// es la definicion del plan.
///
/// Van como clave porque el nombre para mostrar lo traduce el frontend, que ya tiene los
/// 26 en sus dos idiomas. Mandar el nombre desde aqui obligaria a traducirlos dos veces y
/// a que la API leyera Accept-Language solo para esto.
/// </param>
/// <param name="Suscripciones">
/// Cuantas empresas lo tienen contratado. Es lo que convierte esta lista en algo con lo
/// que se puede decidir: un plan con suscripciones no se puede tocar a la ligera, y sin
/// este dato habria que ir a mirar la lista de empresas para saberlo.
/// </param>
public sealed record ResumenPlan(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    decimal PrecioMensual,
    string Moneda,
    int Orden,
    bool Activo,
    DateTime CreadoEn,
    IReadOnlyList<string> Modulos,
    int Suscripciones);

/// <summary>
/// Un modulo del catalogo, para poder armar un plan.
/// </summary>
/// <param name="Numero">
/// El numero de la especificacion funcional: 8 es M8, logistica. Se manda porque es la
/// referencia estable al documento de negocio, y sirve para agrupar el selector.
/// </param>
public sealed record ResumenModulo(string Clave, short Numero, int Orden);

/// <summary>
/// Lo que hace falta para crear un plan.
/// </summary>
/// <param name="Codigo">
/// Identificador estable y en minusculas. Es lo que viaja en el alta de una empresa, asi
/// que cambiarlo despues rompe cualquier automatismo que lo use: se valida al crear y no
/// se puede editar.
/// </param>
/// <param name="Modulos">
/// Las claves de los modulos que incluye. AL MENOS UNA: una empresa sin modulos no puede
/// entrar a nada, asi que un plan vacio es un plan que no se puede vender.
/// </param>
public readonly record struct AltaDePlan(
    string Codigo,
    string Nombre,
    string? Descripcion,
    decimal PrecioMensual,
    string Moneda,
    int Orden,
    IReadOnlyList<string> Modulos);

/// <summary>
/// Distingue los dos desenlaces que le importan al panel. Mismo criterio que
/// <c>ResultadoAlta</c>: un rechazo por validacion es 400 y no un fallo del sistema.
/// </summary>
public readonly record struct ResultadoPlan(bool Correcto, string? Motivo, ResumenPlan? Plan)
{
    public static ResultadoPlan Exito(ResumenPlan plan) => new(true, null, plan);

    public static ResultadoPlan Rechazado(string motivo) => new(false, motivo, null);
}
