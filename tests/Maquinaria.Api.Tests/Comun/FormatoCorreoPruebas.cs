using Maquinaria.Dominio.Comun;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El formato del correo. El alta de empresas solo comprobaba <c>IsNullOrWhiteSpace</c>,
/// asi que "hola" pasaba y la invitacion del administrador se iba a ningun buzon.
///
/// A PROPOSITO no se persigue el RFC 5322: lo que se fija aqui es la forma basica y el
/// rechazo de lo obvio.
/// </summary>
public class FormatoCorreoPruebas
{
    [Theory]
    [InlineData("admin@bajio.mx")]
    [InlineData("a@b.co")]
    [InlineData("ana.lopez+altas@sub.dominio.com.mx")]
    [InlineData("  Admin@Bajio.MX  ")]           // se normaliza antes de validar
    public void Acepta_la_forma_local_arroba_dominio_punto_tld(string correo)
        => Assert.True(FormatoCorreo.EsValido(correo));

    [Theory]
    [InlineData("hola")]                          // la que pasaba antes
    [InlineData("a@b")]                           // sin punto en el dominio
    [InlineData("a@b.c")]                         // extension de una sola letra
    [InlineData("ana lopez@bajio.mx")]            // espacio en el local
    [InlineData("admin@baj io.mx")]               // espacio en el dominio
    [InlineData("admin @bajio.mx")]
    [InlineData("admin@")]
    [InlineData("@bajio.mx")]
    [InlineData("admin@@bajio.mx")]               // dos arrobas
    [InlineData("admin@bajio@mx.com")]
    [InlineData("admin@bajio..mx")]
    [InlineData("admin@bajio.mx.")]               // acaba en punto
    [InlineData("")]
    [InlineData("   ")]
    public void Rechaza_lo_que_no_tiene_forma_de_direccion(string correo)
        => Assert.False(FormatoCorreo.EsValido(correo));

    [Fact]
    public void El_limite_de_254_caracteres_es_exacto()
    {
        // 254 es el largo real de una direccion completa, no un numero inventado.
        const string dominio = "@ejemplo.com";

        var justo = new string('a', FormatoCorreo.LargoMaximo - dominio.Length) + dominio;
        var unoDeMas = new string('a', FormatoCorreo.LargoMaximo - dominio.Length + 1) + dominio;

        Assert.Equal(254, justo.Length);
        Assert.Equal(255, unoDeMas.Length);
        Assert.True(FormatoCorreo.EsValido(justo));
        Assert.False(FormatoCorreo.EsValido(unoDeMas));
    }

    [Theory]
    [InlineData("  Admin@Bajio.MX  ", "admin@bajio.mx")]
    [InlineData("ADMIN@BAJIO.MX", "admin@bajio.mx")]
    [InlineData("admin@bajio.mx", "admin@bajio.mx")]
    public void Normalizar_recorta_y_baja_a_minusculas(string entrada, string esperado)
        => Assert.Equal(esperado, FormatoCorreo.Normalizar(entrada));

    [Fact]
    public void Lo_normalizado_es_lo_que_se_guarda()
    {
        // Es lo que ya hacia el sembrador del administrador. Sin esto, la misma persona
        // acaba con dos cuentas por haber escrito su correo con mayuscula un dia.
        var capturado = "  Admin@Bajio.MX  ";

        Assert.True(FormatoCorreo.EsValido(capturado));
        Assert.Equal("admin@bajio.mx", FormatoCorreo.Normalizar(capturado));
    }
}
