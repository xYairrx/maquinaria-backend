namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// Emite los tokens de acceso. La implementacion (JWT) vive en Infraestructura.
/// </summary>
public interface IProveedorTokens
{
    /// <summary>
    /// Token de un superadministrador de la PLATAFORMA.
    ///
    /// Se emite con una audiencia propia, distinta de la de los usuarios de empresa.
    /// Eso no es cosmetica: impide que un token de plataforma sirva en un endpoint de
    /// empresa o al reves, aunque los firme la misma llave.
    /// </summary>
    TokenEmitido EmitirDePlataforma(Guid usuarioId, string correo, string nombre);

    /// <summary>
    /// Token de un usuario de EMPRESA.
    ///
    /// Lleva el id del tenant —lo necesita el middleware para resolver contra que base
    /// trabajar— pero NUNCA el nombre_bd: un JWT va firmado pero no cifrado, y los
    /// nombres de las bases de los clientes no viajan al navegador.
    ///
    /// Los permisos viajan DENTRO del token, con vigencia corta y refresco rotativo. El
    /// precio aceptado es que revocar un permiso tarda hasta la vigencia en surtir
    /// efecto; a cambio, ninguna peticion consulta la central ni la base de la empresa
    /// para autorizar.
    /// </summary>
    /// <param name="accesoTotal">
    /// Cuando es true, <paramref name="permisos"/> se ignora y no se enumera nada: un
    /// solo claim en lugar de 156.
    /// </param>
    TokenEmitido EmitirDeEmpresa(
        Guid usuarioId,
        string correo,
        string nombre,
        Guid tenantId,
        string slug,
        bool accesoTotal,
        IReadOnlyList<string> permisos);
}

/// <param name="Token">El JWT compacto.</param>
/// <param name="ExpiraEn">Instante UTC de expiracion, para que el cliente sepa cuando renovar.</param>
public readonly record struct TokenEmitido(string Token, DateTime ExpiraEn);
