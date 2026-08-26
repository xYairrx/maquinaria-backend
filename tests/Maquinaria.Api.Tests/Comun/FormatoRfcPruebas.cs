using Maquinaria.Dominio.Comun;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El formato del RFC. La columna <c>rfc</c> es <c>text</c> nullable y SIN CHECK, asi que
/// aqui no hay red debajo: lo que estas pruebas no fijen, la base lo guarda tal cual.
/// El campo aceptaba cualquier longitud y cualquier contenido — eso es lo que se reporto
/// desde la interfaz.
/// </summary>
public class FormatoRfcPruebas
{
    [Theory]
    [InlineData("MDB120315AB1")]                 // 12: persona moral, tres letras
    [InlineData("LOPZ850612H45")]                // 13: persona fisica, cuatro letras
    [InlineData("XAXX010101000")]                // el generico del publico en general
    // Escapes Unicode y no el caracter literal: el archivo se queda en ASCII, como la
    // convencion del proyecto, y la prueba si ejercita la enie de verdad.
    [InlineData("NI\u00D1O850612AB1")]           // enie: valida en un RFC real
    [InlineData("A&B120315XY1")]                 // ampersand: tambien valido
    [InlineData("  mdb120315ab1  ")]             // se normaliza antes de validar
    [InlineData("MDB 120315 AB1")]               // los espacios internos tampoco estorban
    public void Acepta_rfc_de_persona_moral_y_de_persona_fisica(string rfc)
        => Assert.True(FormatoRfc.EsValido(rfc));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MDB12031AB1")]                  // 11: un digito de fecha de menos
    [InlineData("LOPZM850612H45")]               // 14: una letra inicial de mas
    [InlineData("MDBAB0315AB1")]                 // letras en la zona de la fecha
    [InlineData("MDB120315AB")]                  // homoclave incompleta
    [InlineData("MDB-120315-AB1")]               // el guion no es separador de RFC
    [InlineData("MDB12031.AB1")]
    [InlineData("cualquier cosa")]
    [InlineData("' OR 1=1 --")]
    public void Rechaza_lo_que_no_es_un_rfc(string rfc)
        => Assert.False(FormatoRfc.EsValido(rfc));

    [Fact]
    public void Las_dos_longitudes_validas_son_12_y_13()
    {
        // No es un detalle: aceptar solo 13 dejaria fuera a todas las personas morales, que
        // son la mitad de los clientes de una arrendadora.
        Assert.Equal(12, FormatoRfc.Normalizar("MDB120315AB1").Length);
        Assert.Equal(13, FormatoRfc.Normalizar("LOPZ850612H45").Length);
    }

    [Theory]
    [InlineData("  mdb120315ab1  ", "MDB120315AB1")]
    [InlineData("MDB 120315 AB1", "MDB120315AB1")]
    [InlineData("lopz850612h45", "LOPZ850612H45")]
    public void Normalizar_pone_mayusculas_y_quita_todos_los_espacios(
        string entrada, string esperado)
        => Assert.Equal(esperado, FormatoRfc.Normalizar(entrada));

    [Fact]
    public void Lo_normalizado_es_lo_que_se_guarda()
    {
        // El alta guarda el normalizado, no lo que se capturo: si no, dos filas con el
        // mismo RFC escrito distinto no se reconocerian como la misma empresa.
        var capturado = " mdb 120315 ab1 ";

        Assert.True(FormatoRfc.EsValido(capturado));
        Assert.Equal("MDB120315AB1", FormatoRfc.Normalizar(capturado));
    }

    [Fact]
    public void Un_rfc_absurdamente_largo_se_rechaza()
        => Assert.False(FormatoRfc.EsValido(new string('A', 500)));
}
