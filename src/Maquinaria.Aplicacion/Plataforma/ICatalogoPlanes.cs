using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// Lectura y escritura del catalogo comercial: los planes y los modulos que los definen.
///
/// Separado de <c>IRegistroTenants</c> a proposito. Ese registro es del aprovisionamiento
/// —da de alta empresas y mueve su estado— y solo mira los planes de reojo, para resolver
/// el codigo que le llega. Aqui se administra el catalogo, que es otra responsabilidad y
/// otro momento: los planes se definen una vez y se consultan mucho.
/// </summary>
public interface ICatalogoPlanes
{
    /// <summary>
    /// Todos los planes, activos e inactivos, ordenados por <see cref="Plan.Orden"/>.
    ///
    /// Los inactivos SE INCLUYEN: el panel es donde se administra el catalogo, y un plan
    /// retirado hay que poder verlo para reactivarlo. Quien filtra por activo es el alta de
    /// empresas, no esta lista.
    /// </summary>
    Task<IReadOnlyList<ResumenPlan>> ListarPlanesAsync(CancellationToken ct);

    /// <summary>Los modulos activos del catalogo, para armar un plan.</summary>
    Task<IReadOnlyList<ResumenModulo>> ListarModulosAsync(CancellationToken ct);

    Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct);

    /// <summary>
    /// Las claves que NO existen en el catalogo, o que existen inactivas.
    ///
    /// Devuelve las que sobran en lugar de un booleano para poder decir en el mensaje
    /// CUALES estan mal: con un "hay modulos invalidos" a secas, quien captura tiene que
    /// adivinar cual de veintiseis.
    /// </summary>
    Task<IReadOnlyList<string>> ClavesDeModuloDesconocidasAsync(
        IReadOnlyList<string> claves, CancellationToken ct);

    /// <summary>
    /// Inserta el plan y sus modulos EN LA MISMA TRANSACCION.
    ///
    /// Juntos y no por separado por la misma razon que el tenant y su suscripcion: un plan
    /// sin modulos no da acceso a nada, asi que a medias no sirve de nada.
    /// </summary>
    Task<ResumenPlan> CrearAsync(AltaDePlan alta, CancellationToken ct);

    /// <summary>
    /// Retira o reactiva un plan. Devuelve `null` si el codigo no existe.
    ///
    /// Retirar NO toca a quien ya lo tiene contratado: su suscripcion sigue apuntando al
    /// mismo plan con los mismos modulos. Lo unico que cambia es que el alta de empresas
    /// deja de aceptarlo, porque <c>AprovisionarEmpresa</c> exige que este activo.
    /// </summary>
    Task<ResumenPlan?> CambiarActivoAsync(string codigo, bool activo, CancellationToken ct);
}
