namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Crea y migra la base de datos de una empresa. Todo lo que aqui pasa ocurre FUERA de
/// cualquier transaccion de EF Core.
/// </summary>
public interface IAprovisionadorBaseDatos
{
    /// <summary>
    /// PostgreSQL no tiene CREATE DATABASE IF NOT EXISTS, asi que hay que preguntar.
    ///
    /// Sin esto, reintentar un alta que fallo despues de crear la base truena en el
    /// CREATE y el tenant se queda en Fallida para siempre.
    /// </summary>
    Task<bool> ExisteBaseAsync(string nombreBd, CancellationToken ct);

    /// <summary>
    /// CREATE DATABASE. No corre dentro de una transaccion —limitacion de PostgreSQL—
    /// y el identificador se concatena, porque SQL no permite parametrizarlo. De ahi
    /// que el nombre se revalide con regex antes.
    /// </summary>
    Task CrearBaseAsync(string nombreBd, CancellationToken ct);

    /// <summary>
    /// Aplica las migraciones de ContextoEmpresa y devuelve la ultima aplicada, que es
    /// lo que se guarda en tenant.version_esquema.
    ///
    /// Las semillas de permisos y roles van DENTRO de esas migraciones, asi que esto
    /// tambien las siembra. No hace falta codigo de semilla aparte.
    /// </summary>
    Task<string> MigrarAsync(string nombreBd, CancellationToken ct);
}
