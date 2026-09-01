using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Api.Tests.Empresas;

/// <summary>
/// La situacion comercial de una empresa: que los cuatro estados existan de verdad y que
/// suspender signifique algo.
///
/// Hasta el 2026-09-01 no habia forma de mover esta columna: toda empresa nacia en Prueba y
/// nada volvia a escribirla, asi que tres de los cuatro valores del enum eran inalcanzables
/// —el mismo defecto que el trigger del contrato— y la comprobacion de `PuedeOperar` que
/// `MiddlewareTenant` hace en CADA peticion no podia fallar jamas.
///
/// Estas pruebas cubren la REGLA. Lo que no cubren, y hay que decirlo: la validacion del
/// endpoint, porque construir `EmpresasController` arrastra `AprovisionarEmpresa` y
/// `ReenviarInvitacion` con sus seis dependencias, y montar todo eso para comprobar un
/// `Enum.IsDefined` cuesta mas que lo que fija. Lo que si se fija aqui es el supuesto del
/// que depende esa guarda: que los valores validos son exactamente cuatro y ninguno es 0.
/// </summary>
public class EstadoEmpresaPruebas
{
    private static TenantResuelto Empresa(
        EstadoTenant estado,
        EstadoAprovisionamiento aprovisionamiento = EstadoAprovisionamiento.Lista)
        => new(
            Guid.CreateVersion7(), "bajio", "maquinaria_bajio", "Maquinaria del Bajio",
            estado, aprovisionamiento, "America/Mexico_City", "MXN",
            new HashSet<string>(), new Dictionary<string, int>());

    // ------------------------------------------------------------------
    // Que suspender signifique algo
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(EstadoTenant.Prueba)]
    [InlineData(EstadoTenant.Activo)]
    public void Prueba_y_activo_pueden_operar(EstadoTenant estado)
        => Assert.True(Empresa(estado).PuedeOperar);

    /// <summary>
    /// LA PRUEBA QUE JUSTIFICA EL ENDPOINT. Si esto deja de ser cierto, suspender a un
    /// cliente no le corta nada y el panel promete algo que no pasa.
    /// </summary>
    [Theory]
    [InlineData(EstadoTenant.Suspendido)]
    [InlineData(EstadoTenant.Cancelado)]
    public void Suspendido_y_cancelado_NO_pueden_operar(EstadoTenant estado)
        => Assert.False(Empresa(estado).PuedeOperar);

    /// <summary>
    /// El estado comercial no basta: una empresa activa cuya base no esta lista tampoco
    /// opera. Abrir una base a medio aprovisionar daria errores de tabla inexistente en vez
    /// de un mensaje claro.
    /// </summary>
    [Theory]
    [InlineData(EstadoAprovisionamiento.Pendiente)]
    [InlineData(EstadoAprovisionamiento.Creando)]
    [InlineData(EstadoAprovisionamiento.Fallida)]
    public void Ni_una_empresa_activa_opera_si_su_base_no_esta_lista(
        EstadoAprovisionamiento aprovisionamiento)
        => Assert.False(Empresa(EstadoTenant.Activo, aprovisionamiento).PuedeOperar);

    // ------------------------------------------------------------------
    // El supuesto del que depende la guarda del endpoint
    // ------------------------------------------------------------------

    /// <summary>
    /// SON EXACTAMENTE CUATRO Y VAN DEL 1 AL 4, que es lo que el CHECK de la migracion
    /// impone en la base. Agregar un quinto valor al enum sin migrar el CHECK dejaria una
    /// fila que la aplicacion acepta y el motor rechaza, y el error saldria en produccion.
    /// </summary>
    [Fact]
    public void Los_estados_son_cuatro_y_ninguno_es_cero()
    {
        var valores = Enum.GetValues<EstadoTenant>().Select(e => (short)e).Order().ToList();

        Assert.Equal([(short)1, (short)2, (short)3, (short)4], valores);
    }

    /// <summary>
    /// Lo que la guarda del endpoint tiene que rechazar.
    ///
    /// `System.Text.Json` mete CUALQUIER numero en un enum sin quejarse, asi que sin el
    /// `Enum.IsDefined` un cuerpo con `{"estado": 0}` llegaria hasta el UPDATE y reventaria
    /// contra el CHECK como un 500, cuando es dato mal capturado y es un 400.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Un_numero_fuera_del_enum_no_esta_definido(short valor)
        => Assert.False(Enum.IsDefined((EstadoTenant)valor));

    [Fact]
    public void Los_cuatro_estados_si_estan_definidos()
        => Assert.All(Enum.GetValues<EstadoTenant>(), e => Assert.True(Enum.IsDefined(e)));
}
