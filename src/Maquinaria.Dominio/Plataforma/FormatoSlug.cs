using System.Text.RegularExpressions;

namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Valida el identificador publico de una empresa.
///
/// EL MISMO patron que el CHECK tenant_slug_formato de la base, repetido aqui a
/// proposito. La base es la ultima linea de defensa, no la primera: sin esta
/// validacion, un slug mal formado llega hasta el INSERT y sale como un error 500
/// generico en lugar de un mensaje que diga que esta mal. Eso paso — se detecto
/// probando el alta de empresas.
///
/// Nota sobre el patron: NO admite guiones bajos, solo guiones. El guion bajo aparece
/// en <see cref="Tenant.NombreBd"/>, que se DERIVA del slug reemplazandolos, porque un
/// nombre de base con guiones obliga a entrecomillar el identificador en cada
/// sentencia. Son dos formatos distintos y es facil confundirlos.
/// </summary>
public static partial class FormatoSlug
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex Patron();

    /// <summary>Longitud minima, contando los extremos que el patron exige.</summary>
    public const int LargoMinimo = 3;

    public const int LargoMaximo = 50;

    /// <summary>El slug se normaliza antes de validar: sin espacios y en minusculas.</summary>
    public static bool EsValido(string slug) => Patron().IsMatch(Normalizar(slug));

    public static string Normalizar(string slug) => slug.Trim().ToLowerInvariant();

    /// <summary>
    /// Mensaje para el usuario. No repite el valor recibido: si viniera de una entrada
    /// hostil, propagarlo al log o a la respuesta es justo lo que no se quiere.
    /// </summary>
    public const string Explicacion =
        "El identificador de la empresa debe tener entre 3 y 50 caracteres, "
        + "solo minusculas, digitos y guiones, y empezar y terminar con letra o digito.";
}
