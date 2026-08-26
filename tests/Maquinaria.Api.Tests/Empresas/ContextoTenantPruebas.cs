using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Api.Tests;

public class TenantResueltoPruebas
{
    private static TenantResuelto Tenant(
        EstadoTenant estado = EstadoTenant.Activo,
        EstadoAprovisionamiento aprov = EstadoAprovisionamiento.Lista,
        string[]? modulos = null,
        Dictionary<string, int>? limites = null)
        => new(
            Guid.CreateVersion7(),
            "bajio",
            "maquinaria_bajio",
            "Maquinaria del Bajio SA de CV",
            estado,
            aprov,
            "America/Mexico_City",
            "MXN",
            (modulos ?? ["rentas", "cotizaciones"]).ToHashSet(),
            limites ?? []);

    [Theory]
    [InlineData(EstadoTenant.Prueba, true)]
    [InlineData(EstadoTenant.Activo, true)]
    [InlineData(EstadoTenant.Suspendido, false)]
    [InlineData(EstadoTenant.Cancelado, false)]
    public void Solo_prueba_y_activo_pueden_operar(EstadoTenant estado, bool esperado)
        => Assert.Equal(esperado, Tenant(estado: estado).PuedeOperar);

    [Theory]
    [InlineData(EstadoAprovisionamiento.Pendiente)]
    [InlineData(EstadoAprovisionamiento.Creando)]
    [InlineData(EstadoAprovisionamiento.Fallida)]
    public void Una_base_a_medio_aprovisionar_no_puede_operar(EstadoAprovisionamiento aprov)
    {
        // Abrirla daria errores de tabla inexistente en lugar de un mensaje claro.
        Assert.False(Tenant(aprov: aprov).PuedeOperar);
    }

    [Fact]
    public void El_limite_del_tenant_gana_sobre_el_valor_por_defecto()
    {
        var t = Tenant(limites: new Dictionary<string, int> { [ClavesLimite.MaxEquipos] = 300 });

        Assert.Equal(300, t.LimiteEfectivo(ClavesLimite.MaxEquipos, TipoLimite.Ilimitado));
    }

    [Fact]
    public void Sin_fila_propia_se_hereda_el_valor_por_defecto()
    {
        // La tabla tenant_limite es dispersa: no tener fila es lo normal.
        Assert.Equal(
            TipoLimite.Ilimitado,
            Tenant().LimiteEfectivo(ClavesLimite.MaxEquipos, TipoLimite.Ilimitado));
    }

    [Fact]
    public void Cero_es_un_cupo_valido_y_distinto_de_no_tener_fila()
    {
        var t = Tenant(limites: new Dictionary<string, int> { [ClavesLimite.MaxUsuarios] = 0 });

        Assert.Equal(0, t.LimiteEfectivo(ClavesLimite.MaxUsuarios, TipoLimite.Ilimitado));
    }

    [Fact]
    public void La_compuerta_de_modulos_responde_por_clave()
    {
        var t = Tenant(modulos: ["rentas"]);

        Assert.True(t.IncluyeModulo(ClavesModulo.Rentas));
        Assert.False(t.IncluyeModulo(ClavesModulo.Logistica));
    }
}
