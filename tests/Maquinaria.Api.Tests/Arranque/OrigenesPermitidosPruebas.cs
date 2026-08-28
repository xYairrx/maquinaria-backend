using Maquinaria.Api.Arranque;

namespace Maquinaria.Api.Tests;

/// <summary>
/// Con un subdominio por empresa, CORS deja de ser una lista y pasa a ser un
/// predicado. Un predicado mal escrito es una puerta abierta silenciosa: acepta un
/// dominio ajeno y nada falla ni se registra, asi que solo estas pruebas lo detectan.
///
/// El caso que justifica el archivo entero es 'malo-ejemplo.com': con un
/// EndsWith("ejemplo.com") sin el punto del prefijo, pasaria.
/// </summary>
public class OrigenesPermitidosPruebas
{
    private static OpcionesCors Opciones(
        string dominioBase = "ejemplo.com",
        bool exigirHttps = true,
        params string[] origenes)
        => new() { DominioBase = dominioBase, ExigirHttps = exigirHttps, Origenes = origenes };

    [Theory]
    [InlineData("https://bajio.ejemplo.com")]
    [InlineData("https://demo.ejemplo.com")]
    [InlineData("https://login.ejemplo.com")]
    [InlineData("https://ejemplo.com")]                  // el dominio pelado tambien
    [InlineData("https://BAJIO.EJEMPLO.COM")]            // el anfitrion no distingue mayusculas
    [InlineData("https://a.b.ejemplo.com")]              // subdominio anidado
    public void Acepta_subdominios_del_dominio_base(string origen)
        => Assert.True(OrigenesPermitidos.EsPermitido(origen, Opciones()));

    [Theory]
    [InlineData("https://malo-ejemplo.com")]             // EL CASO: termina en -ejemplo.com
    [InlineData("https://ejemplo.com.malo.com")]         // el dominio real va de sufijo
    [InlineData("https://ejemplo.como")]
    [InlineData("https://otrodominio.com")]
    [InlineData("https://bajio.ejemplo.com.evil.io")]
    public void Rechaza_dominios_ajenos_que_se_le_parecen(string origen)
        => Assert.False(OrigenesPermitidos.EsPermitido(origen, Opciones()));

    [Fact]
    public void Rechaza_http_cuando_se_exige_https()
        => Assert.False(OrigenesPermitidos.EsPermitido(
            "http://bajio.ejemplo.com", Opciones(exigirHttps: true)));

    [Fact]
    public void Acepta_http_en_desarrollo()
        => Assert.True(OrigenesPermitidos.EsPermitido(
            "http://bajio.localhost:4200", Opciones("localhost", exigirHttps: false)));

    [Theory]
    [InlineData("file://bajio.ejemplo.com")]
    [InlineData("ftp://bajio.ejemplo.com")]
    public void Rechaza_esquemas_que_no_son_web(string origen)
        => Assert.False(OrigenesPermitidos.EsPermitido(origen, Opciones(exigirHttps: false)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-una-url")]
    [InlineData("null")]                                  // lo que manda un sandbox
    public void Rechaza_origenes_que_no_son_absolutos(string origen)
        => Assert.False(OrigenesPermitidos.EsPermitido(origen, Opciones()));

    [Fact]
    public void Sin_dominio_base_solo_vale_la_lista_exacta()
    {
        var opciones = Opciones(dominioBase: "", origenes: "https://panel.otro.com");

        Assert.True(OrigenesPermitidos.EsPermitido("https://panel.otro.com", opciones));
        Assert.False(OrigenesPermitidos.EsPermitido("https://bajio.ejemplo.com", opciones));
    }

    [Fact]
    public void La_lista_exacta_convive_con_el_dominio_base()
    {
        var opciones = Opciones(origenes: "https://panel.otro.com");

        Assert.True(OrigenesPermitidos.EsPermitido("https://panel.otro.com", opciones));
        Assert.True(OrigenesPermitidos.EsPermitido("https://bajio.ejemplo.com", opciones));
    }

    [Fact]
    public void Un_subdominio_inexistente_se_acepta_a_proposito()
    {
        // No se consulta la base: verificar que el slug sea un cliente real costaria una
        // consulta por preflight y delataria que slugs existen. Que el tenant exista lo
        // resuelve la peticion, no el CORS.
        Assert.True(OrigenesPermitidos.EsPermitido(
            "https://empresa-que-no-existe.ejemplo.com", Opciones()));
    }
}
