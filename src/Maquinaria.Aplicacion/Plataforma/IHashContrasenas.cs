namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// Hashea y verifica contrasenas. La implementacion vive en Infraestructura porque
/// depende de una libreria de criptografia; el caso de uso solo conoce esto.
/// </summary>
public interface IHashContrasenas
{
    /// <summary>
    /// Devuelve el hash en un formato AUTODESCRIPTIVO que incluye el algoritmo y
    /// sus parametros, para que subir el costo mas adelante no invalide los hashes
    /// existentes.
    /// </summary>
    string Hash(string contrasena);

    /// <summary>
    /// Verifica en tiempo constante respecto al contenido de la contrasena.
    /// </summary>
    ResultadoVerificacion Verificar(string hashAlmacenado, string contrasena);

    /// <summary>
    /// Consume el mismo tiempo que una verificacion real contra un hash senuelo.
    ///
    /// NO es decorativo: si al no existir la cuenta se respondiera de inmediato y al
    /// existir se tardara ~200 ms hasheando, esa diferencia es medible y revela que
    /// correos son cuentas reales. Hay que gastar el mismo tiempo siempre.
    /// </summary>
    void VerificarSenuelo(string contrasena);
}

/// <param name="EsValida">Si la contrasena corresponde al hash.</param>
/// <param name="NecesitaRehash">
/// True cuando el hash es valido pero se genero con parametros mas debiles que los
/// actuales. El caso de uso aprovecha el unico momento en que tiene la contrasena en
/// claro —un login exitoso— para volver a guardarla con el costo nuevo.
/// </param>
public readonly record struct ResultadoVerificacion(bool EsValida, bool NecesitaRehash);
