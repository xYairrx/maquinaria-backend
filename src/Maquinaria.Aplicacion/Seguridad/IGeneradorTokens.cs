namespace Maquinaria.Aplicacion.Seguridad;

/// <summary>
/// Genera tokens de un solo uso —invitaciones y restablecimientos— y su hash.
///
/// SEPARADO DE IHashContrasenas A PROPOSITO, y no es duplicacion. Una contrasena es un
/// secreto de BAJA ENTROPIA elegido por una persona, asi que hay que estirarlo con 600
/// mil iteraciones de PBKDF2 para que probar candidatos sea caro. Un token de 256 bits
/// generado por un CSPRNG ya es imposible de adivinar: estirarlo no agrega seguridad y
/// si agrega 200 ms a cada validacion de liga.
///
/// Para estos, SHA-256 sobre el token es suficiente. El hash existe para que leer la
/// base no de ligas usables, no para resistir un ataque de diccionario que no aplica.
/// </summary>
public interface IGeneradorTokens
{
    /// <summary>
    /// Devuelve el token en claro —el unico momento en que existe— y su hash, que es
    /// lo unico que se guarda.
    /// </summary>
    TokenGenerado Generar();

    /// <summary>Para validar una liga entrante contra lo guardado.</summary>
    string Hashear(string tokenEnClaro);
}

/// <param name="EnClaro">Va en la liga del correo. No se guarda en ninguna parte.</param>
/// <param name="Hash">Lo que se guarda en token_acceso.hash_token.</param>
public readonly record struct TokenGenerado(string EnClaro, string Hash);
