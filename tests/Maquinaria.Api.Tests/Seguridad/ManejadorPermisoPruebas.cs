using System.Security.Claims;
using Maquinaria.Api.Seguridad;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authorization;

namespace Maquinaria.Api.Tests.Seguridad;

/// <summary>
/// La compuerta de permisos, que es lo unico que separa a un usuario de empresa de los
/// endpoints que no le tocan.
///
/// Se prueba sin base de datos y sin HTTP a proposito: el manejador solo lee claims, y los
/// claims los emite <c>ProveedorTokensJwt</c>. Lo que hay que comprobar es la decision, no
/// la tuberia.
/// </summary>
public class ManejadorPermisoPruebas
{
    private static async Task<bool> Concede(string clave, params Claim[] claims)
    {
        var requisito = new RequisitoPermiso(clave);

        var usuario = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba"));

        var contexto = new AuthorizationHandlerContext([requisito], usuario, resource: null);

        await new ManejadorPermiso().HandleAsync(contexto);

        return contexto.HasSucceeded;
    }

    private static Claim Permisos(string valor) => new(ProveedorTokensJwt.ClaimPermisos, valor);

    private static Claim AccesoTotal() => new(ProveedorTokensJwt.ClaimAccesoTotal, "true");

    [Fact]
    public async Task Concede_cuando_el_permiso_esta_en_la_lista()
        => Assert.True(await Concede(
            "equipos.crear", Permisos("rentas.consultar equipos.crear clientes.editar")));

    [Fact]
    public async Task Concede_cuando_es_el_unico_permiso()
        => Assert.True(await Concede("equipos.crear", Permisos("equipos.crear")));

    [Fact]
    public async Task Niega_cuando_el_permiso_no_esta()
        => Assert.False(await Concede(
            "equipos.eliminar", Permisos("equipos.consultar equipos.crear")));

    /// <summary>
    /// SIN CLAIM NO HAY PERMISO. Es el caso de un token de plataforma o de uno de empresa
    /// cuyo rol no concede nada: la ausencia del claim se lee como cero permisos, no como
    /// «no aplica».
    /// </summary>
    [Fact]
    public async Task Niega_cuando_no_hay_claim_de_permisos()
        => Assert.False(await Concede("equipos.consultar"));

    [Fact]
    public async Task Niega_cuando_el_claim_viene_vacio()
        => Assert.False(await Concede("equipos.consultar", Permisos("")));

    /// <summary>
    /// EL PREFIJO NO ALCANZA, y por eso la comparacion es de palabra completa. Si fuera por
    /// prefijo, <c>equipos.editar</c> concederia <c>equipos.editar-todo</c> el dia que exista
    /// un permiso con ese nombre.
    /// </summary>
    [Theory]
    [InlineData("equipos.editar-todo")]
    [InlineData("equipos.edit")]
    [InlineData("equipos.editarX")]
    public async Task Niega_cuando_solo_coincide_el_prefijo(string concedido)
        => Assert.False(await Concede("equipos.editar", Permisos(concedido)));

    /// <summary>
    /// Un espacio de mas, al principio o al final, no debe cambiar nada: el claim lo arma
    /// un <c>string.Join</c> y una lista vacia intermedia produciria dobles espacios.
    /// </summary>
    [Theory]
    [InlineData(" equipos.crear ")]
    [InlineData("equipos.crear  rentas.crear")]
    [InlineData("  equipos.crear")]
    public async Task Tolera_espacios_de_mas(string concedidos)
        => Assert.True(await Concede("equipos.crear", Permisos(concedidos)));

    /// <summary>
    /// ACCESO TOTAL SALTA LA VERIFICACION. El administrador de la empresa lo trae y NO trae
    /// el claim de permisos: enumerarlos seria una lista que se desincroniza del catalogo.
    /// </summary>
    [Fact]
    public async Task Acceso_total_concede_sin_claim_de_permisos()
        => Assert.True(await Concede("lo.que.sea", AccesoTotal()));

    /// <summary>
    /// Y solo el valor exacto "true": un claim presente con otro valor no es acceso total.
    /// </summary>
    [Theory]
    [InlineData("false")]
    [InlineData("True")]
    [InlineData("1")]
    public async Task Acceso_total_solo_con_true_exacto(string valor)
        => Assert.False(await Concede(
            "equipos.crear", new Claim(ProveedorTokensJwt.ClaimAccesoTotal, valor)));
}
