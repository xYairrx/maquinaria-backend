using System.Text.RegularExpressions;

namespace Maquinaria.Dominio.Comun;

/// <summary>
/// El formato del RFC mexicano, para las TRES entidades que lo llevan:
/// <c>Tenant.Rfc</c> en Plataforma, y <c>Cliente.Rfc</c> y <c>Proveedor.Rfc</c> en
/// Terceros. Vive en Comun y no en Plataforma justamente por eso: si estuviera junto
/// al tenant, Terceros tendria que mirar hacia Plataforma para validar un dato que no
/// es de plataforma.
///
/// Mismo criterio que <c>FormatoSlug</c>: la columna <c>rfc</c> es <c>text</c> nullable
/// y SIN CHECK, asi que aqui no hay ultima linea de defensa detras. Lo que no se
/// rechace en este metodo se guarda tal cual, y un RFC de tres caracteres o con la
/// fecha llena de letras no vuelve a detectarse nunca.
///
/// LA ENIE Y EL '&amp;' ESTAN EN EL PATRON A PROPOSITO: los dos son validos en un RFC
/// mexicano real —hay razones sociales con enie y con ampersand— y un patron que solo
/// admita A-Z rechaza empresas legitimas. La enie va como escape <c>\u00D1</c> para que
/// el archivo se quede en ASCII, igual que hacen las pruebas del slug.
/// </summary>
public static partial class FormatoRfc
{
    /// <summary>
    /// Tres o cuatro letras, seis digitos de fecha y tres de homoclave: 12 caracteres
    /// para persona moral y 13 para persona fisica. LOS DOS SON VALIDOS aqui — una
    /// empresa de renta de maquinaria puede estar dada de alta de cualquiera de las dos
    /// formas, y aceptar solo 13 dejaria fuera a la mitad.
    /// </summary>
    [GeneratedRegex("^[A-Z\u00D1&]{3,4}[0-9]{6}[A-Z0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex Patron();

    /// <summary>
    /// Mayusculas y sin ningun espacio, ni de los extremos ni internos. Se normaliza
    /// ANTES de validar y lo normalizado es lo que se guarda: un RFC capturado como
    /// "abc 123456 def" es el mismo dato, y rechazarlo por algo que la maquina arregla
    /// sola solo sirve para que alguien lo deje vacio.
    /// </summary>
    public static string Normalizar(string rfc)
        => string.Concat(rfc.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    public static bool EsValido(string rfc) => Patron().IsMatch(Normalizar(rfc));

    /// <summary>
    /// Mensaje para el usuario. NO repite el valor recibido: si viniera de una entrada
    /// hostil, propagarlo al log o a la respuesta es justo lo que no se quiere.
    /// </summary>
    public const string Explicacion =
        "El RFC debe tener 12 caracteres para persona moral o 13 para persona fisica: "
        + "tres o cuatro letras, seis digitos de fecha y tres de homoclave.";
}
