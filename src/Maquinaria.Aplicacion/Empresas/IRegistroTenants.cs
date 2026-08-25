using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Escrituras sobre la base central que necesita el aprovisionamiento.
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

    Task MarcarListaAsync(Guid tenantId, string versionEsquema, CancellationToken ct);

    /// <summary>
    /// Escribe SOLO version_esquema. Lo usa migrar-empresas, que no debe tocar
    /// estado_aprovisionamiento: una empresa en Fallida por un correo que no salio sigue
    /// estando en Fallida despues de migrarla, y marcarla Lista aqui esconderia el
    /// problema.
    /// </summary>
    Task ActualizarVersionEsquemaAsync(
        Guid tenantId, string versionEsquema, CancellationToken ct);

    /// <summary>
    /// Las empresas vivas con su base y su version de esquema. La usan migrar-empresas y
    /// el endpoint de salud de esquemas.
    ///
    /// EXCLUYE las dadas de baja logica, al contrario que <see cref="ListarAsync"/>: no
    /// hay que migrar la base de una empresa que ya no opera, y como el historial es
    /// append-only, si algun dia vuelve, alcanza.
    /// </summary>
    Task<IReadOnlyList<EmpresaConEsquema>> ListarConEsquemaAsync(CancellationToken ct);

    /// <summary>
    /// Todas las empresas, para el panel. Incluye las dadas de baja: su historial y su
    /// base siguen existiendo, y esconderlas del panel solo dificultaria auditarlas.
    /// </summary>
    Task<IReadOnlyList<ResumenEmpresa>> ListarAsync(CancellationToken ct);

    /// <summary>
    /// Las empresas que el migrador tiene que recorrer, con su nombre_bd.
    ///
    /// Separado de ListarAsync a proposito: ese devuelve un resumen para el panel y NO
    /// lleva nombre_bd, porque ese dato no tiene por que salir del servidor.
    /// </summary>
    Task<IReadOnlyList<TenantParaMigrar>> ListarParaMigrarAsync(
        string? slug, CancellationToken ct);

    /// <summary>
    /// Solo la version del esquema. Distinto de MarcarListaAsync, que ademas cambia el
    /// estado de aprovisionamiento: migrar una empresa que ya operaba no debe tocar su
    /// estado.
    /// </summary>
    Task MarcarVersionEsquemaAsync(Guid tenantId, string version, CancellationToken ct);

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
