using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Api.Tests.Comun;

/// <summary>
/// El acotado del tamano de pagina y el calculo del salto.
///
/// Vale una prueba porque <see cref="Filtro"/> se enlaza desde la CADENA DE CONSULTA: los
/// valores llegan de fuera y pueden ser cero, negativos o absurdos, y lo que los Servicios
/// usan es <c>TamanoEfectivo</c>, no <c>Tamano</c>. Si el acotado se rompe,
/// <c>?tamano=1000000</c> vuelve a traer la tabla entera.
/// </summary>
public class FiltroPruebas
{
    [Fact]
    public void Por_defecto_pagina_uno_y_cincuenta_filas()
    {
        var filtro = new Filtro();

        Assert.Equal(1, filtro.Numero);
        Assert.Equal(Filtro.TamanoPorDefecto, filtro.TamanoEfectivo);
        Assert.Equal(0, filtro.Saltar);
    }

    [Theory]
    [InlineData(1_000_000)]
    [InlineData(201)]
    public void El_tamano_no_pasa_del_maximo(int pedido)
        => Assert.Equal(Filtro.TamanoMaximo, new Filtro { Tamano = pedido }.TamanoEfectivo);

    /// <summary>Cero y negativos caen a uno, no a cero: una pagina de cero filas no existe.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void El_tamano_no_baja_de_uno(int pedido)
        => Assert.Equal(1, new Filtro { Tamano = pedido }.TamanoEfectivo);

    [Theory]
    [InlineData(1, 50, 0)]
    [InlineData(2, 50, 50)]
    [InlineData(3, 20, 40)]
    public void El_salto_se_calcula_sobre_el_tamano_efectivo(
        int numero, int tamano, int esperado)
        => Assert.Equal(esperado, new Filtro { Numero = numero, Tamano = tamano }.Saltar);

    /// <summary>
    /// Una pagina cero o negativa se trata como la primera. Es un parametro de interfaz mal
    /// armado, no algo que merezca rechazar una consulta.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Una_pagina_invalida_no_produce_un_salto_negativo(int numero)
        => Assert.Equal(0, new Filtro { Numero = numero }.Saltar);

    /// <summary>
    /// El salto de una pagina alta con el tamano acotado usa el tamano YA acotado: si usara
    /// el pedido, pagina 2 con tamano 1000 saltaria mil filas y devolveria vacio.
    /// </summary>
    [Fact]
    public void El_salto_usa_el_tamano_acotado_y_no_el_pedido()
        => Assert.Equal(
            Filtro.TamanoMaximo, new Filtro { Numero = 2, Tamano = 1_000 }.Saltar);

    [Fact]
    public void La_pagina_calcula_cuantas_hay()
    {
        var pagina = new Pagina<int>([1, 2, 3], Numero: 1, Tamano: 20, Total: 41);

        Assert.Equal(3, pagina.Paginas);
    }

    /// <summary>Un listado sin coincidencias es una pagina vacia, no un 404.</summary>
    [Fact]
    public void Una_pagina_vacia_no_tiene_paginas()
    {
        var pagina = Pagina<int>.Vacia(1, 50);

        Assert.Empty(pagina.Filas);
        Assert.Equal(0, pagina.Total);
        Assert.Equal(0, pagina.Paginas);
    }
}
