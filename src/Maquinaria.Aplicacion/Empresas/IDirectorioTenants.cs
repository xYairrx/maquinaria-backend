namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Resuelve empresas desde la base central, con cache.
///
/// Dos entradas porque hay dos momentos distintos: el LOGIN llega con el slug que la
/// persona escribio, y toda peticion posterior llega con el id que viaja en el JWT.
/// </summary>
public interface IDirectorioTenants
{
    /// <summary>Para el login. El slug se normaliza dentro.</summary>
    Task<TenantResuelto?> BuscarPorSlugAsync(string slug, CancellationToken ct);

    /// <summary>Para las peticiones ya autenticadas.</summary>
    Task<TenantResuelto?> BuscarPorIdAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Tira la entrada de cache de una empresa. Hay que llamarlo al suspenderla,
    /// cambiarle el plan o moverle un limite, o seguiria operando con lo anterior
    /// hasta que expire el TTL.
    ///
    /// OJO: invalida la cache de ESTA instancia. Con varias instancias en Railway,
    /// cada una tiene la suya, asi que lo que de verdad acota el desfase es el TTL.
    /// Una invalidacion distribuida solo se justifica cuando el desfase moleste.
    /// </summary>
    void Invalidar(Guid tenantId, string slug);
}
