using System.Text.RegularExpressions;

namespace Maquinaria.Dominio.Comun;

/// <summary>
/// El formato de un telefono capturado a mano. Lo llevan <c>Tenant.Telefono</c>,
/// <c>Cliente.Telefono</c>, <c>Proveedor.Telefono</c>, <c>Trabajador.Telefono</c> y
/// <c>Ubicacion.Telefono</c>, asi que vive en Comun y no en la carpeta de ninguno.
///
/// La columna es <c>text</c> sin CHECK: sin esta comprobacion, "llamar al Beto" es un
/// telefono valido para la base. La queja concreta que llego desde la interfaz fue esa,
/// que el campo aceptaba letras.
///
/// SOLO DIGITOS. La version anterior admitia " + ( ) -" como separadores, razonando que el
/// formato varia por pais y que aplanarlo pierde lo que alguien escribio a proposito. Se
/// cambio por peticion expresa, y el argumento nuevo es mejor: un telefono es una secuencia
/// de digitos y el formato es cosa de como se PINTA, no de como se guarda. Guardando
/// "(477) 123 4567" y "4771234567" como valores distintos, el mismo telefono son dos, y el
/// dia que haya que buscar por telefono o comparar dos fichas no coinciden.
/// </summary>
public static partial class FormatoTelefono
{
    [GeneratedRegex("^[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Patron();

    /// <summary>
    /// Solo recorta las puntas. NO limpia separadores, y esa es la decision: si esto los
    /// quitara, "477.123.4567" pasaria la validacion, y entonces "solo digitos" seria
    /// mentira en el unico sitio donde se puede hacer cumplir.
    ///
    /// La amabilidad de limpiar lo que alguien PEGA vive en el campo de la pantalla, que
    /// filtra mientras se escribe. Aqui, que es la frontera, se rechaza. Un cliente que
    /// mande separadores recibe el mensaje y sabe exactamente que arreglar.
    /// </summary>
    public static string Normalizar(string telefono) => telefono.Trim();

    /// <summary>Diez digitos es un numero nacional mexicano completo, con lada.</summary>
    public const int DigitosMinimos = 10;

    /// <summary>Quince es el maximo de E.164, asi que esto cubre cualquier pais.</summary>
    public const int DigitosMaximos = 15;

    /// <summary>
    /// Digitos y nada mas. Se rechazan las letras —que es lo que se reporto: el campo daba
    /// por bueno "llamar al Beto"— y tambien los separadores.
    /// </summary>
    public static bool EsValido(string telefono)
    {
        var recortado = Normalizar(telefono);

        return Patron().IsMatch(recortado)
            && recortado.Length is >= DigitosMinimos and <= DigitosMaximos;
    }

    /// <summary>
    /// Mensaje para el usuario. NO repite el valor recibido: si viniera de una entrada
    /// hostil, propagarlo al log o a la respuesta es justo lo que no se quiere.
    /// </summary>
    public const string Explicacion =
        "El telefono debe ser de 10 a 15 digitos, sin letras ni simbolos.";
}
