using Maquinaria.Dominio.Plataforma;
using Maquinaria.Infraestructura.Persistencia;

namespace Maquinaria.Api.Tests;

/// <summary>
/// La validacion del nombre de base es CONTROL DE SEGURIDAD, no cosmetica: es lo que
/// se ejecuta justo antes de concatenar un identificador en un CREATE DATABASE.
/// </summary>
public class FabricaConexionesEmpresaPruebas
{
    [Theory]
    [InlineData("maquinaria_bajio")]
    [InlineData("maquinaria_norte_2")]
    [InlineData("abc")]
    public void Acepta_nombres_validos(string nombre)
        => FabricaConexionesEmpresa.ValidarNombreBd(nombre);

    [Theory]
    [InlineData("")]
    [InlineData("ab")]                              // menos de 3
    [InlineData("1empieza_con_digito")]
    [InlineData("Mayusculas")]
    [InlineData("con-guiones")]                     // obligarian a entrecomillar
    [InlineData("con espacio")]
    [InlineData("punto.y_coma")]
    [InlineData("maquinaria\"; DROP DATABASE postgres; --")]
    [InlineData("maquinaria_bajio; SELECT 1")]
    public void Rechaza_nombres_invalidos(string nombre)
        => Assert.Throws<ArgumentException>(
            () => FabricaConexionesEmpresa.ValidarNombreBd(nombre));

    [Fact]
    public void El_mensaje_de_error_no_repite_el_valor_recibido()
    {
        // Si el valor viniera de una entrada hostil, repetirlo en el mensaje lo
        // propagaria a los logs y a las respuestas.
        const string hostil = "'; DROP DATABASE postgres; --";

        var e = Assert.Throws<ArgumentException>(
            () => FabricaConexionesEmpresa.ValidarNombreBd(hostil));

        Assert.DoesNotContain("DROP", e.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("central")]
    [InlineData("plantilla")]
    [InlineData("PLANTILLA")]
    [InlineData(" postgres ")]
    [InlineData("admin")]
    [InlineData("www")]
    public void Los_slugs_reservados_se_detectan(string slug)
        => Assert.True(SlugsReservados.EstaReservado(slug));

    [Theory]
    [InlineData("bajio")]
    [InlineData("maquinaria-norte")]
    [InlineData("constructora-lopez")]
    public void Un_slug_normal_no_esta_reservado(string slug)
        => Assert.False(SlugsReservados.EstaReservado(slug));
}
