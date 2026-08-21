namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Que se le exige a una contrasena.
///
/// SOLO LONGITUD, sin reglas de composicion —una mayuscula, un digito, un simbolo—.
/// Es lo que recomienda el NIST desde 2017 y no es laxitud: las reglas de composicion
/// empujan a la gente a "Password1!" y a apuntarla en un papel, mientras que la
/// longitud es lo que de verdad multiplica el espacio de busqueda.
///
/// 12 caracteres con PBKDF2 a 600 mil iteraciones deja el ataque por diccionario fuera
/// de alcance practico.
/// </summary>
public static class PoliticaContrasena
{
    public const int LargoMinimo = 12;

    /// <summary>
    /// Tope alto, no bajo. Existe solo para que nadie pueda mandar un megabyte y
    /// hacernos gastar 600 mil iteraciones de PBKDF2 sobre el: es proteccion contra
    /// abuso, no una restriccion al usuario.
    /// </summary>
    public const int LargoMaximo = 256;

    public static bool EsValida(string? contrasena)
        => contrasena is not null
        && contrasena.Length >= LargoMinimo
        && contrasena.Length <= LargoMaximo;

    public static string Explicacion =>
        $"La contrasena debe tener al menos {LargoMinimo} caracteres.";
}
