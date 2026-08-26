using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Api.Tests;

/// <summary>
/// La deteccion de desfase de esquema. Es la unica logica no trivial del bloque de
/// migraciones y es PURA, asi que se prueba sin base de datos.
///
/// Lo que esta en juego: las migraciones de empresa se aplican N veces, una por base, y
/// si esta comparacion se equivoca el panel dice que todo esta al dia mientras una
/// empresa opera dos versiones atras.
/// </summary>
public class EstadoEsquemaPruebas
{
    /// <summary>Los ids reales del proyecto, en el orden en que EF Core los devuelve.</summary>
    private static readonly string[] Disponibles =
    [
        "20260821191557_EmpresaInicial",
        "20260821191816_EmpresaSemillaSeguridad",
        "20260821192954_EmpresaAuditoriaYConfiguracion",
        "20260821205930_EmpresaPermisosModulosCompletos",
        "20260824232637_EmpresaCatalogosOrganizacion",
    ];

    [Fact]
    public void Una_empresa_al_dia_no_esta_desfasada()
    {
        var comparacion = ComparadorEsquema.Comparar(Disponibles[^1], Disponibles);

        Assert.False(comparacion.Desfasada);
        Assert.Equal(0, comparacion.MigracionesPendientes);
        Assert.True(comparacion.VersionReconocida);
        Assert.Equal(Disponibles[^1], comparacion.VersionAplicada);
        Assert.Equal(Disponibles[^1], comparacion.VersionDisponible);
    }

    [Theory]
    [InlineData(3, 1)]  // el caso real: demo y bajio, una migracion atras
    [InlineData(2, 2)]
    [InlineData(0, 4)]
    public void Cuenta_las_migraciones_que_faltan(int indiceAplicado, int pendientesEsperadas)
    {
        var comparacion = ComparadorEsquema.Comparar(Disponibles[indiceAplicado], Disponibles);

        Assert.True(comparacion.Desfasada);
        Assert.Equal(pendientesEsperadas, comparacion.MigracionesPendientes);
        Assert.True(comparacion.VersionReconocida);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_version_aplicada_cuenta_como_desfasada_y_no_reconocida(string? version)
    {
        // Un tenant sin version_esquema es un alta que no llego a migrar. Tratarlo como
        // "al dia" lo esconderia justo en el reporte que existe para encontrarlo.
        var comparacion = ComparadorEsquema.Comparar(version, Disponibles);

        Assert.True(comparacion.Desfasada);
        Assert.Equal(Disponibles.Length, comparacion.MigracionesPendientes);
        Assert.False(comparacion.VersionReconocida);
        Assert.Null(comparacion.VersionAplicada);
    }

    [Fact]
    public void Una_version_que_el_codigo_no_conoce_se_reporta_como_no_reconocida()
    {
        // Pasa con una base POR DELANTE del binario que responde: se desplego una
        // version vieja de la API. No se inventa un numero de pendientes.
        var comparacion = ComparadorEsquema.Comparar("20270101000000_EmpresaDelFuturo", Disponibles);

        Assert.False(comparacion.VersionReconocida);
        Assert.False(comparacion.Desfasada);
        Assert.Equal(0, comparacion.MigracionesPendientes);
        Assert.Equal("20270101000000_EmpresaDelFuturo", comparacion.VersionAplicada);
    }

    [Fact]
    public void Solo_el_nombre_sin_marca_de_tiempo_no_se_reconoce()
    {
        // version_esquema guarda el ID COMPLETO, que es lo que devuelve EF Core. Adivinar
        // por el nombre suelto convertiria un dato mal escrito a mano en un "al dia".
        var comparacion = ComparadorEsquema.Comparar("EmpresaCatalogosOrganizacion", Disponibles);

        Assert.False(comparacion.VersionReconocida);
    }

    [Fact]
    public void Los_espacios_alrededor_se_toleran()
    {
        var comparacion = ComparadorEsquema.Comparar($"  {Disponibles[^1]}  ", Disponibles);

        Assert.True(comparacion.VersionReconocida);
        Assert.False(comparacion.Desfasada);
    }

    [Fact]
    public void La_comparacion_es_sensible_a_mayusculas()
    {
        // Un id de migracion es un identificador, no texto de usuario.
        var comparacion = ComparadorEsquema.Comparar(
            Disponibles[^1].ToUpperInvariant(), Disponibles);

        Assert.False(comparacion.VersionReconocida);
    }

    [Fact]
    public void Manda_la_posicion_en_la_lista_y_no_el_orden_alfabetico()
    {
        // El orden de aplicacion es el del historial de EF Core, no el de comparar
        // cadenas. Si algun dia una migracion se genera con un id menor al de la
        // anterior, lo que decide sigue siendo la posicion.
        string[] fueraDeOrden = ["30000000_Ultima", "10000000_Primera"];

        var comparacion = ComparadorEsquema.Comparar("30000000_Ultima", fueraDeOrden);

        Assert.True(comparacion.Desfasada);
        Assert.Equal(1, comparacion.MigracionesPendientes);
        Assert.Equal("10000000_Primera", comparacion.VersionDisponible);
    }

    [Fact]
    public void Sin_migraciones_en_el_codigo_nadie_esta_desfasado()
    {
        // No puede pasar en produccion, pero el reporte no debe reventar por ello.
        var comparacion = ComparadorEsquema.Comparar(null, []);

        Assert.False(comparacion.Desfasada);
        Assert.Equal(0, comparacion.MigracionesPendientes);
        Assert.Null(comparacion.VersionDisponible);
    }

    [Fact]
    public void El_reporte_de_migracion_solo_falla_por_los_fallos_reales()
    {
        // OMITIDA no cuenta para el codigo de salida: un alta que nunca creo su base no
        // debe hacer fallar el comando en cada corrida.
        var reporte = new ReporteMigracion(
        [
            new ResultadoEmpresa("bajio", DesenlaceMigracion.Migrada, Disponibles[4], "1 migraciones"),
            new ResultadoEmpresa("nueva", DesenlaceMigracion.Omitida, null, "Su base no existe."),
            new ResultadoEmpresa("centro", DesenlaceMigracion.AlDia, Disponibles[4], null),
        ]);

        Assert.False(reporte.HuboFallas);
        Assert.Equal(3, reporte.Total);
        Assert.Equal(1, reporte.AlDia);
        Assert.Equal(1, reporte.Migradas);
        Assert.Equal(1, reporte.Omitidas);
        Assert.Equal(0, reporte.Fallidas);
    }

    [Fact]
    public void Una_empresa_fallida_hace_fallar_el_comando()
    {
        var reporte = new ReporteMigracion(
        [
            new ResultadoEmpresa("bajio", DesenlaceMigracion.Migrada, Disponibles[4], null),
            new ResultadoEmpresa("demo", DesenlaceMigracion.Fallida, null, "timeout"),
        ]);

        Assert.True(reporte.HuboFallas);
        Assert.Equal(1, reporte.Fallidas);
    }
}
