using System.Text.RegularExpressions;

namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// El formato de la clave de un <see cref="TipoLimite"/>.
///
/// NO SE REUSA <c>FormatoCodigoPlan</c>, y la razon es una sola letra: ese admite guiones y
/// NO admite guiones bajos, asi que rechazaria <c>max_equipos</c> — las cuatro claves que el
/// sistema ya tiene, escritas en <see cref="ClavesLimite"/>. Un formato que no acepta los
/// valores que ya estan en la base no es una validacion, es un error a punto de pasar.
///
/// Se admite el guion bajo y NO el guion normal, para que haya una sola forma de escribir
/// una clave: con las dos permitidas, <c>max-equipos</c> y <c>max_equipos</c> serian dos
/// filas distintas que nadie distingue de un vistazo.
/// </summary>
public static partial class FormatoClaveLimite
{
    public const int LargoMaximo = 40;

    public const string Explicacion =
        "La clave debe ir en minusculas, con digitos y guiones bajos, empezar y acabar en "
        + "letra o digito, y no pasar de 40 caracteres. Por ejemplo: max_equipos.";

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_]*[a-z0-9]$|^[a-z0-9]$")]
    private static partial Regex Patron();

    /// <summary>
    /// Recorta y baja a minusculas ANTES de validar, por lo mismo que el codigo de un plan:
    /// una clave escrita con mayusculas o con espacios de sobra se arregla sola en lugar de
    /// rechazarse.
    /// </summary>
    public static string Normalizar(string clave) => clave.Trim().ToLowerInvariant();

    public static bool EsValido(string clave)
        => clave.Length is > 0 and <= LargoMaximo && Patron().IsMatch(clave);

    /// <summary>
    /// Si hay CODIGO detras de esta clave, o solo una fila con un nombre bonito.
    ///
    /// Es la pregunta mas importante de todo el catalogo de limites, y por eso vive en el
    /// dominio y viaja en el DTO. <see cref="TipoLimite"/> ya lo avisa: que el tipo sea una
    /// fila no hace que el limite funcione. Un tipo cuya clave no este en
    /// <see cref="ClavesLimite"/> se puede crear, se puede editar y se le puede fijar un
    /// cupo a cada empresa — y no va a acotar nada nunca, porque no hay codigo que lo lea.
    /// </summary>
    public static bool EsReconocida(string clave)
        => ClavesLimite.Todas.Contains(clave, StringComparer.Ordinal);
}
