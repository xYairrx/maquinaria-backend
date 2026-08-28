using Maquinaria.Aplicacion.Plataforma;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maquinaria.Api.Tests;

/// <summary>
/// Crear un plan del catalogo comercial.
///
/// Lo que se fija aqui son las validaciones que impiden meter en el catalogo un plan que no
/// se puede vender. La mas importante es la del plan SIN MODULOS: el plan ES su conjunto de
/// modulos, asi que uno vacio produce una empresa que entra y no ve ni una pantalla — y eso
/// no da ningun error, solo un cliente confundido.
/// </summary>
public class CrearPlanPruebas
{
    private static AltaDePlan Alta(
        string codigo = "profesional",
        string nombre = "Plan profesional",
        decimal precio = 1200m,
        string moneda = "MXN",
        IReadOnlyList<string>? modulos = null)
        => new(codigo, nombre, null, precio, moneda, 10, modulos ?? ["equipos", "rentas"]);

    private static CrearPlan Caso(CatalogoFalso catalogo)
        => new(catalogo, NullLogger<CrearPlan>.Instance);

    [Fact]
    public async Task Crea_un_plan_valido()
    {
        var catalogo = new CatalogoFalso();

        var r = await Caso(catalogo).EjecutarAsync(Alta(), CancellationToken.None);

        Assert.True(r.Correcto);
        Assert.Equal("profesional", r.Plan!.Codigo);
        Assert.Equal(2, r.Plan.Modulos.Count);
        Assert.Single(catalogo.Creados);
    }

    [Fact]
    public async Task Normaliza_el_codigo_y_la_moneda_antes_de_guardar()
    {
        // Con el codigo sin normalizar, 'Profesional' y 'profesional' serian dos planes.
        var catalogo = new CatalogoFalso();

        var r = await Caso(catalogo)
            .EjecutarAsync(Alta(codigo: "  Profesional ", moneda: "mxn"), CancellationToken.None);

        Assert.True(r.Correcto);
        Assert.Equal("profesional", catalogo.Creados[0].Codigo);
        Assert.Equal("MXN", catalogo.Creados[0].Moneda);
    }

    [Fact]
    public async Task Rechaza_un_plan_sin_modulos()
    {
        // Un plan vacio no da acceso a nada. La empresa que lo contrate entra y no ve ni una
        // pantalla, sin ningun error de por medio.
        var r = await Caso(new CatalogoFalso())
            .EjecutarAsync(Alta(modulos: []), CancellationToken.None);

        Assert.False(r.Correcto);
        Assert.Contains("al menos un modulo", r.Motivo);
    }

    [Fact]
    public async Task Rechaza_un_codigo_repetido_diciendo_cual()
    {
        var catalogo = new CatalogoFalso { CodigosExistentes = ["profesional"] };

        var r = await Caso(catalogo).EjecutarAsync(Alta(), CancellationToken.None);

        Assert.False(r.Correcto);
        Assert.Contains("profesional", r.Motivo);
        Assert.Empty(catalogo.Creados);
    }

    [Fact]
    public async Task Rechaza_modulos_desconocidos_y_dice_cuales_son()
    {
        // Con un "hay modulos invalidos" a secas, quien captura tiene que adivinar cual de
        // veintiseis esta mal escrito.
        var catalogo = new CatalogoFalso { Desconocidas = ["equiposs", "rentaz"] };

        var r = await Caso(catalogo)
            .EjecutarAsync(Alta(modulos: ["equiposs", "rentaz"]), CancellationToken.None);

        Assert.False(r.Correcto);
        Assert.Contains("equiposs", r.Motivo);
        Assert.Contains("rentaz", r.Motivo);
    }

    [Theory]
    [InlineData("Codigo Invalido", "minusculas")]
    [InlineData("", "minusculas")]
    public async Task Rechaza_un_codigo_con_mal_formato(string codigo, string pista)
    {
        var r = await Caso(new CatalogoFalso())
            .EjecutarAsync(Alta(codigo: codigo), CancellationToken.None);

        Assert.False(r.Correcto);
        Assert.Contains(pista, r.Motivo);
    }

    [Fact]
    public async Task Rechaza_un_nombre_vacio()
    {
        var r = await Caso(new CatalogoFalso())
            .EjecutarAsync(Alta(nombre: "   "), CancellationToken.None);

        Assert.False(r.Correcto);
        Assert.Contains("nombre", r.Motivo);
    }

    [Fact]
    public async Task Rechaza_un_precio_negativo()
    {
        // El CHECK de la base tambien lo impide, pero llegar hasta el INSERT convierte un
        // dato mal capturado en un 500 generico.
        var r = await Caso(new CatalogoFalso())
            .EjecutarAsync(Alta(precio: -1m), CancellationToken.None);

        Assert.False(r.Correcto);
        Assert.Contains("negativo", r.Motivo);
    }

    [Fact]
    public async Task Acepta_precio_cero()
    {
        // Cero es valido: es un plan de cortesia o de prueba, distinto de no tener precio.
        var r = await Caso(new CatalogoFalso())
            .EjecutarAsync(Alta(precio: 0m), CancellationToken.None);

        Assert.True(r.Correcto);
    }

    [Theory]
    [InlineData("MX")]
    [InlineData("PESOS")]
    [InlineData("")]
    public async Task Rechaza_una_moneda_que_no_es_ISO_4217(string moneda)
    {
        var r = await Caso(new CatalogoFalso())
            .EjecutarAsync(Alta(moneda: moneda), CancellationToken.None);

        Assert.False(r.Correcto);
        Assert.Contains("tres letras", r.Motivo);
    }

    [Fact]
    public async Task Quita_modulos_repetidos()
    {
        // La misma clave dos veces violaria la llave (PlanId, ModuloId) de plan_modulo.
        var catalogo = new CatalogoFalso();

        var r = await Caso(catalogo)
            .EjecutarAsync(Alta(modulos: ["equipos", "equipos", "rentas"]), CancellationToken.None);

        Assert.True(r.Correcto);
        Assert.Equal(2, catalogo.Creados[0].Modulos.Count);
    }

    private sealed class CatalogoFalso : ICatalogoPlanes
    {
        public List<AltaDePlan> Creados { get; } = [];

        public string[] CodigosExistentes { get; init; } = [];

        public string[] Desconocidas { get; init; } = [];

        public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct)
            => Task.FromResult(CodigosExistentes.Contains(codigo));

        public Task<IReadOnlyList<string>> ClavesDeModuloDesconocidasAsync(
            IReadOnlyList<string> claves, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(
                Desconocidas.Where(claves.Contains).ToArray());

        public Task<ResumenPlan> CrearAsync(AltaDePlan alta, CancellationToken ct)
        {
            Creados.Add(alta);

            return Task.FromResult(new ResumenPlan(
                Guid.CreateVersion7(),
                alta.Codigo,
                alta.Nombre,
                alta.Descripcion,
                alta.PrecioMensual,
                alta.Moneda,
                alta.Orden,
                true,
                DateTime.UtcNow,
                alta.Modulos,
                0));
        }

        public Task<IReadOnlyList<ResumenPlan>> ListarPlanesAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ResumenPlan>>([]);

        public Task<IReadOnlyList<ResumenModulo>> ListarModulosAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ResumenModulo>>([]);

        public Task<ResumenPlan?> CambiarActivoAsync(
            string codigo, bool activo, CancellationToken ct)
            => Task.FromResult<ResumenPlan?>(null);
    }
}
