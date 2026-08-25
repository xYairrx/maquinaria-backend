using Maquinaria.Aplicacion.Plataforma;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El formato del codigo de un plan.
///
/// Importa mas de lo que parece: el codigo viaja en el alta de una empresa
/// (<c>AltaDeEmpresa.CodigoPlan</c>) y no se puede editar despues, asi que un codigo con
/// mayusculas o con un espacio de mas se convierte en un identificador que nadie escribe
/// igual dos veces.
/// </summary>
public class FormatoCodigoPlanPruebas
{
    [Theory]
    [InlineData("base")]
    [InlineData("profesional")]
    [InlineData("basico-anual")]
    [InlineData("plan2026")]
    [InlineData("a")]
    public void Acepta_minusculas_digitos_y_guiones_internos(string codigo)
        => Assert.True(FormatoCodigoPlan.EsValido(codigo));

    [Theory]
    [InlineData("")]
    [InlineData("-base")]
    [InlineData("base-")]
    [InlineData("con espacio")]
    [InlineData("con_guion_bajo")]
    [InlineData("acentuado-ñ")]
    [InlineData("Mayuscula")]
    [InlineData("con.punto")]
    public void Rechaza_lo_que_no_se_escribe_igual_dos_veces(string codigo)
        => Assert.False(FormatoCodigoPlan.EsValido(codigo));

    [Fact]
    public void Rechaza_lo_demasiado_largo()
    {
        var largo = new string('a', FormatoCodigoPlan.LargoMaximo + 1);

        Assert.False(FormatoCodigoPlan.EsValido(largo));
        Assert.True(FormatoCodigoPlan.EsValido(largo[..FormatoCodigoPlan.LargoMaximo]));
    }

    [Theory]
    [InlineData("  BASE  ", "base")]
    [InlineData("Profesional", "profesional")]
    [InlineData("basico-anual", "basico-anual")]
    public void Normalizar_recorta_y_baja_a_minusculas(string entrada, string esperado)
        => Assert.Equal(esperado, FormatoCodigoPlan.Normalizar(entrada));

    [Fact]
    public void Normalizar_arregla_lo_que_la_maquina_puede_arreglar_sola()
    {
        // Se normaliza ANTES de validar, asi que un codigo escrito con mayusculas se acepta
        // en lugar de rechazarse por algo que no es culpa de quien captura.
        Assert.False(FormatoCodigoPlan.EsValido("  Profesional "));
        Assert.True(FormatoCodigoPlan.EsValido(FormatoCodigoPlan.Normalizar("  Profesional ")));
    }
}
