using System.Text.RegularExpressions;

namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// El formato del codigo de un plan. Vive aparte para poder probarlo sin base de datos,
/// igual que <c>FormatoSlug</c>.
///
/// NO es un slug de tenant y no se reusa <c>FormatoSlug</c>: ese valida algo que va a ser
/// un subdominio —de ahi su largo maximo y sus reservados como 'admin' o 'api'— y un codigo
/// de plan no viaja en ningun host. Lo que si comparten es el criterio: minusculas, digitos
/// y guiones, para que el codigo se pueda escribir a mano en una configuracion o en un
/// script sin sorpresas de mayusculas ni de acentos.
/// </summary>
public static partial class FormatoCodigoPlan
{
    public const int LargoMaximo = 40;

    public const string Explicacion =
        "El codigo debe ir en minusculas, con digitos y guiones, empezar y acabar en letra "
        + "o digito, y no pasar de 40 caracteres.";

    /// <summary>
    /// Minusculas, digitos y guiones internos. Se compila una vez con
    /// <c>GeneratedRegex</c>, que la construye en tiempo de compilacion.
    /// </summary>
    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$")]
    private static partial Regex Patron();

    /// <summary>
    /// Recorta y baja a minusculas. Se normaliza ANTES de validar para que un codigo
    /// escrito con mayusculas o con espacios de sobra se acepte en lugar de rechazarse por
    /// algo que la maquina puede arreglar sola.
    /// </summary>
    public static string Normalizar(string codigo) => codigo.Trim().ToLowerInvariant();

    public static bool EsValido(string codigo)
        => codigo.Length is > 0 and <= LargoMaximo && Patron().IsMatch(codigo);
}
