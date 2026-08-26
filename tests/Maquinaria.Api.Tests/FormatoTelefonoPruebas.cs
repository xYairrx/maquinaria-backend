using Maquinaria.Dominio.Comun;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El formato del telefono. La queja concreta que llego desde la interfaz fue que el campo
/// aceptaba letras; la columna es <c>text</c> sin CHECK, asi que "llamar al Beto" se
/// guardaba como telefono y ahi se quedaba.
/// </summary>
public class FormatoTelefonoPruebas
{
    [Theory]
    [InlineData("4771234567")]                   // 10 digitos: nacional con lada
    [InlineData("525551234567")]                 // 12: con codigo de pais, tambien en digitos
    [InlineData("123456789012345")]              // 15: el maximo de E.164
    [InlineData(" 4771234567 ")]                 // solo las puntas se recortan
    public void Acepta_de_10_a_15_digitos(string telefono)
        => Assert.True(FormatoTelefono.EsValido(telefono));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789")]                    // 9: uno de menos
    [InlineData("1234567890123456")]             // 16: uno de mas
    [InlineData("477 123 45")]
    [InlineData("llamar al Beto")]               // la queja, literal
    [InlineData("477123456O")]                   // una letra O donde iba un cero
    [InlineData("ext 4771234567")]
    [InlineData("477.123.4567")]
    [InlineData("(477) 123-4567")]               // los separadores ya NO se admiten
    [InlineData("477 123 4567")]
    [InlineData("+52 477 123 4567")]
    [InlineData("477/123/4567")]
    [InlineData("' OR 1=1 --")]
    public void Rechaza_letras_y_los_largos_fuera_de_rango(string telefono)
        => Assert.False(FormatoTelefono.EsValido(telefono));

    [Fact]
    public void Los_limites_exactos_del_rango()
    {
        Assert.False(FormatoTelefono.EsValido(new string('4', FormatoTelefono.DigitosMinimos - 1)));
        Assert.True(FormatoTelefono.EsValido(new string('4', FormatoTelefono.DigitosMinimos)));
        Assert.True(FormatoTelefono.EsValido(new string('4', FormatoTelefono.DigitosMaximos)));
        Assert.False(FormatoTelefono.EsValido(new string('4', FormatoTelefono.DigitosMaximos + 1)));
    }

    [Fact]
    public void Solo_separadores_no_es_un_telefono()
        => Assert.False(FormatoTelefono.EsValido("+() - "));

    [Fact]
    public void El_mismo_telefono_solo_tiene_UNA_forma_valida()
    {
        // Esta prueba decia lo contrario hasta el 2026-08-26: las tres formas eran validas
        // y el alta guardaba lo que se capturara. Se cambio por peticion expresa, y el
        // argumento nuevo es mejor: guardando "(477) 123 4567" y "4771234567" como valores
        // distintos, el mismo telefono son dos, y el dia que haya que buscar por telefono o
        // comparar dos fichas no coinciden. El formato es cosa de como se PINTA.
        Assert.True(FormatoTelefono.EsValido("4771234567"));
        Assert.False(FormatoTelefono.EsValido("(477) 123-4567"));
        Assert.False(FormatoTelefono.EsValido("+52 477 123 4567"));
    }

    [Fact]
    public void Normalizar_recorta_las_puntas_y_NO_limpia_separadores()
    {
        // Si limpiara, "477.123.4567" pasaria la validacion y «solo digitos» seria mentira
        // en el unico sitio donde se puede hacer cumplir. Limpiar lo que alguien PEGA es
        // trabajo del campo en pantalla, no de la frontera.
        Assert.Equal("4771234567", FormatoTelefono.Normalizar("  4771234567  "));
        Assert.Equal("477.123.4567", FormatoTelefono.Normalizar("477.123.4567"));
    }
}
