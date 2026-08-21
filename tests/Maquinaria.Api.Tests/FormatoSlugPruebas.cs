using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El formato del slug se valida en la aplicacion y no solo en el CHECK de la base.
/// Sin estas pruebas, un slug mal formado sale como un 500 generico — que es
/// literalmente lo que pasaba antes de escribirlas.
/// </summary>
public class FormatoSlugPruebas
{
    [Theory]
    [InlineData("bajio")]
    [InlineData("demo")]
    [InlineData("maquinaria-norte")]
    [InlineData("constructora-lopez-2026")]
    [InlineData("a1b")]
    [InlineData("  BAJIO  ")]                    // se normaliza antes de validar
    public void Acepta_slugs_validos(string slug)
        => Assert.True(FormatoSlug.EsValido(slug));

    [Theory]
    [InlineData("")]
    [InlineData("ab")]                            // menos de 3
    [InlineData("-empieza-con-guion")]
    [InlineData("termina-con-guion-")]
    [InlineData("con_guion_bajo")]                // el guion bajo es de nombre_bd, no del slug
    [InlineData("con espacio")]
    [InlineData("con.punto")]
    // Escapes Unicode y no los caracteres literales: el archivo se queda en ASCII,
    // como pide la convencion, y la prueba si ejercita el caracter real.
    [InlineData("acentuado-ni\u00F1o")]
    [InlineData("acentuado-b\u00E1jio")]
    [InlineData("' OR 1=1 --")]
    public void Rechaza_slugs_invalidos(string slug)
        => Assert.False(FormatoSlug.EsValido(slug));

    [Fact]
    public void Un_slug_demasiado_largo_se_rechaza()
        => Assert.False(FormatoSlug.EsValido(new string('a', 51)));

    [Fact]
    public void El_limite_de_longitud_se_acepta()
        => Assert.True(FormatoSlug.EsValido(new string('a', 50)));

    [Theory]
    [InlineData("maquinaria-norte", "maquinaria_maquinaria_norte")]
    [InlineData("demo", "maquinaria_demo")]
    public void El_nombre_de_base_deriva_del_slug(string slug, string esperado)
    {
        // Los guiones pasan a guiones bajos: un nombre de base con guiones obligaria a
        // entrecomillar el identificador en cada sentencia.
        var nombreBd = Maquinaria.Aplicacion.Empresas.AprovisionarEmpresa
            .NombreBdDesdeSlug(slug);

        Assert.Equal(esperado, nombreBd);
    }

    [Fact]
    public void Todo_slug_valido_produce_un_nombre_de_base_valido()
    {
        // La propiedad que de verdad importa: si el slug pasa, el nombre_bd derivado
        // tiene que pasar tambien, o el alta fallaria en el CREATE DATABASE.
        foreach (var slug in new[] { "abc", "demo", "maquinaria-norte", new string('z', 50) })
        {
            var nombreBd = Maquinaria.Aplicacion.Empresas.AprovisionarEmpresa
                .NombreBdDesdeSlug(slug);

            Maquinaria.Infraestructura.Persistencia.FabricaConexionesEmpresa
                .ValidarNombreBd(nombreBd);
        }
    }
}
