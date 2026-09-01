using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El formato de la clave de un tipo de limite, y —lo que de verdad importa— si esa clave
/// tiene CODIGO detras.
/// </summary>
public class FormatoClaveLimitePruebas
{
    [Theory]
    [InlineData("max_equipos")]
    [InlineData("max_almacenamiento_gb")]
    [InlineData("a1")]
    [InlineData("x")]
    public void Acepta_minusculas_digitos_y_guiones_bajos(string clave)
        => Assert.True(FormatoClaveLimite.EsValido(clave));

    [Theory]
    // El guion NORMAL se rechaza a proposito: con los dos permitidos, `max-equipos` y
    // `max_equipos` serian dos filas distintas que nadie distingue de un vistazo.
    [InlineData("max-equipos")]
    [InlineData("Max_Equipos")]
    [InlineData("_max")]
    [InlineData("max_")]
    [InlineData("max equipos")]
    [InlineData("máx_equipos")]
    [InlineData("")]
    public void Rechaza_lo_demas(string clave)
        => Assert.False(FormatoClaveLimite.EsValido(clave));

    [Fact]
    public void Rechaza_lo_mas_largo_que_el_maximo()
        => Assert.False(
            FormatoClaveLimite.EsValido(new string('a', FormatoClaveLimite.LargoMaximo + 1)));

    [Fact]
    public void Normaliza_antes_de_validar()
        => Assert.True(FormatoClaveLimite.EsValido(FormatoClaveLimite.Normalizar("  MAX_EQUIPOS ")));

    /// <summary>
    /// LA PRUEBA QUE IMPORTA. Si esto se rompe, el panel deja de avisar de que un tipo
    /// inventado no acota nada, y alguien va a fijar un cupo confiando en un tope que no
    /// existe.
    /// </summary>
    [Fact]
    public void Las_cuatro_claves_del_sistema_estan_reconocidas()
        => Assert.All(ClavesLimite.Todas, c => Assert.True(FormatoClaveLimite.EsReconocida(c)));

    [Theory]
    [InlineData("max_obras")]
    [InlineData("max_equipo")]
    [InlineData("MAX_EQUIPOS")]
    public void Una_clave_inventada_NO_esta_reconocida(string clave)
        => Assert.False(FormatoClaveLimite.EsReconocida(clave));
}
