using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Escrituras sobre la base central: las del aprovisionamiento y las que mueven la
/// situacion comercial de una empresa.
/// </summary>
public interface IRegistroTenants
{
    Task<bool> ExisteSlugAsync(string slug, CancellationToken ct);

    Task<Plan?> BuscarPlanPorCodigoAsync(string codigo, CancellationToken ct);

    /// <summary>
    /// Inserta el tenant y su suscripcion EN LA MISMA TRANSACCION.
    ///
    /// Juntos y no por separado porque un tenant sin suscripcion no tiene ningun
    /// modulo contratado: arrancaria sin poder entrar a nada. La suscripcion no es un
    /// paso opcional del alta.
    /// </summary>
    Task CrearAsync(Tenant tenant, Suscripcion suscripcion, CancellationToken ct);

    Task CambiarEstadoAprovisionamientoAsync(
        Guid tenantId, EstadoAprovisionamiento estado, CancellationToken ct);

    /// <summary>
    /// Mueve la SITUACION COMERCIAL de una empresa: prueba, activa, suspendida, cancelada.
    ///
    /// NO ES EstadoAprovisionamiento, que cuenta otra cosa —si su base se creo bien— y se
    /// mueve solo. Este lo decide una persona.
    ///
    /// Hasta el 2026-09-01 no existia, y eso dejaba TRES DE LOS CUATRO valores del enum
    /// inalcanzables: toda empresa nacia en Prueba y nada volvia a escribir la columna. De
    /// paso dejaba sin poder ejercerse la comprobacion de `PuedeOperar` que
    /// `MiddlewareTenant` hace en CADA peticion, escrita expresamente para que suspender a
    /// un cliente surta efecto sin esperar a que caduquen sus tokens.
    ///
    /// Devuelve el resumen ya actualizado, o `null` si el slug no existe.
    /// </summary>
    Task<ResumenEmpresa?> CambiarEstadoAsync(
        string slug, EstadoTenant estado, CancellationToken ct);

    Task MarcarListaAsync(Guid tenantId, string versionEsquema, CancellationToken ct);

    /// <summary>
    /// Todas las empresas, para el panel. Incluye las dadas de baja: su historial y su
    /// base siguen existiendo, y esconderlas del panel solo dificultaria auditarlas.
    /// </summary>
    Task<IReadOnlyList<ResumenEmpresa>> ListarAsync(CancellationToken ct);

    /// <summary>
    /// Las empresas que el migrador tiene que recorrer, con su nombre_bd. La usan
    /// migrar-empresas y el endpoint de salud de esquemas, que revisa sin tocar.
    ///
    /// Separado de ListarAsync a proposito: ese devuelve un resumen para el panel y NO
    /// lleva nombre_bd, porque ese dato no tiene por que salir del servidor.
    ///
    /// EXCLUYE las dadas de baja logica, al contrario que <see cref="ListarAsync"/>: no
    /// hay que migrar la base de una empresa que ya no opera, y como el historial es
    /// append-only, si algun dia vuelve, alcanza.
    /// </summary>
    Task<IReadOnlyList<TenantParaMigrar>> ListarParaMigrarAsync(
        string? slug, CancellationToken ct);

    /// <summary>
    /// Solo la version del esquema. Distinto de MarcarListaAsync, que ademas cambia el
    /// estado de aprovisionamiento: migrar una empresa que ya operaba no debe tocar su
    /// estado.
    /// </summary>
    Task MarcarVersionEsquemaAsync(Guid tenantId, string version, CancellationToken ct);

    /// <summary>
    /// Deja registrado como quedo el ultimo intento de mandar la invitacion.
    ///
    /// Se llama SIEMPRE, con true y con false: escribir solo los exitos dejaria el fallo
    /// indistinguible de «todavia no se ha intentado», que es el estado del que este campo
    /// vino a sacarnos.
    /// </summary>
    Task MarcarInvitacionEnviadaAsync(Guid tenantId, bool enviada, CancellationToken ct);

    /// <summary>Para reintentar un alta que quedo a medias.</summary>
    Task<Tenant?> BuscarPorSlugAsync(string slug, CancellationToken ct);

    /// <summary>
    /// Si la excepcion es una violacion de unicidad de la base.
    ///
    /// Vive detras de la interfaz porque reconocerla exige saber de Npgsql —el
    /// SQLSTATE 23505— y Aplicacion no depende de infraestructura. Sin esto, la
    /// alternativa seria que el caso de uso atrapara DbUpdateException, que es
    /// exactamente el tipo que esta capa no debe conocer.
    /// </summary>
    bool EsColisionDeUnicidad(Exception e);
}
