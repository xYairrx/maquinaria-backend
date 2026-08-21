using System.Security.Cryptography;
using System.Text;
using Maquinaria.Aplicacion.Seguridad;

namespace Maquinaria.Infraestructura.Seguridad;

/// <summary>
/// Tokens de un solo uso: 256 bits de un CSPRNG, en base64url, y SHA-256 como hash.
///
/// SHA-256 Y NO PBKDF2, y la diferencia con las contrasenas es deliberada. Una
/// contrasena es de baja entropia y hay que estirarla para que probar candidatos sea
/// caro. Un token de 256 bits aleatorios no se adivina: estirarlo no agrega seguridad y
/// si le agrega 200 ms a cada apertura de liga. El hash aqui existe para que leer la
/// base no de ligas usables, no para resistir un diccionario que no aplica.
///
/// base64url y no base64: el token viaja en un query string y los caracteres '+', '/'
/// y '=' obligarian a codificarlo dos veces.
/// </summary>
public sealed class GeneradorTokensAleatorios : IGeneradorTokens
{
    private const int BytesToken = 32;

    public TokenGenerado Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(BytesToken);
        var enClaro = Base64UrlSinRelleno(bytes);

        return new TokenGenerado(enClaro, Hashear(enClaro));
    }

    public string Hashear(string tokenEnClaro)
        => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(tokenEnClaro)));

    private static string Base64UrlSinRelleno(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
