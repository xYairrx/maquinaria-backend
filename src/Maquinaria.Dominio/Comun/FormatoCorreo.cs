using System.Text.RegularExpressions;

namespace Maquinaria.Dominio.Comun;

/// <summary>
/// La forma minima de una direccion de correo: <c>local@dominio.tld</c>.
///
/// Vive en Comun porque el correo esta en media docena de entidades —
/// <c>Usuario.Correo</c>, <c>Cliente.Correo</c>, <c>Tenant.CorreoContacto</c>— y ninguna
/// carpeta es la duena natural. Y no hay CHECK en la base, asi que sin esto "hola" es una
/// direccion valida: el alta de empresas solo comprobaba <c>IsNullOrWhiteSpace</c> y por
/// ahi entraba.
///
/// A PROPOSITO NO INTENTA CUBRIR EL RFC 5322. Las regex que lo persiguen son ilegibles,
/// nadie las revisa dos veces y de todas formas siguen estando mal —el RFC admite
/// comentarios entre parentesis y literales entrecomillados—. Lo que si sirve es exigir
/// la forma basica y rechazar lo obvio: si la direccion existe o no se sabe cuando la
/// invitacion llega, o no llega.
/// </summary>
public static partial class FormatoCorreo
{
    /// <summary>
    /// Algo, una sola arroba, y un dominio con al menos un punto y una extension de dos
    /// letras o mas. <c>\s</c> queda fuera de las dos clases negadas, asi que un espacio
    /// en cualquier posicion —incluido el medio— no pasa.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(?:\.[^@\s.]+)*\.[a-z]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex Patron();

    /// <summary>El limite real de una direccion completa, no un numero inventado.</summary>
    public const int LargoMaximo = 254;

    /// <summary>
    /// Minusculas y recortado, que es lo que ya hace el sembrador del administrador. Se
    /// normaliza ANTES de validar y lo normalizado es lo que se guarda, para que la misma
    /// persona no acabe con dos cuentas por haber escrito su correo con mayuscula un dia.
    /// </summary>
    public static string Normalizar(string correo) => correo.Trim().ToLowerInvariant();

    /// <summary>
    /// El largo se comprueba antes que el patron: no hace falta correr una regex sobre una
    /// cadena de un megabyte para saber que no es un correo.
    /// </summary>
    public static bool EsValido(string correo)
    {
        var normalizado = Normalizar(correo);

        return normalizado.Length is > 0 and <= LargoMaximo && Patron().IsMatch(normalizado);
    }

    /// <summary>
    /// Mensaje para el usuario. NO repite el valor recibido: si viniera de una entrada
    /// hostil, propagarlo al log o a la respuesta es justo lo que no se quiere.
    /// </summary>
    public const string Explicacion =
        "El correo debe tener la forma nombre@dominio.com: sin espacios, con un punto en "
        + "el dominio, una extension de al menos dos letras y no mas de 254 caracteres.";
}
