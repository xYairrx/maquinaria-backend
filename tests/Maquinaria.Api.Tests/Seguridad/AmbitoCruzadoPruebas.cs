using System.Reflection;
using Maquinaria.Api.Arranque;
using Maquinaria.Api.Seguridad;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Maquinaria.Api.Tests.Seguridad;

/// <summary>
/// QUE UN TOKEN DE PLATAFORMA NO SIRVA EN UNA EMPRESA, NI AL REVES.
///
/// Los dos tokens los firma LA MISMA LLAVE. Lo unico que los separa son la audiencia y el
/// claim `ambito`, y lo unico que exige esa separacion son las policies declaradas en cada
/// controlador. Hasta el 2026-09-01 no habia ni una prueba sobre eso: quitarle el
/// `[Authorize(PoliticasAutorizacion.Empresa)]` a un controlador —o igualar las dos
/// audiencias en configuracion— dejaba la suite en verde y abria el paso de una poblacion
/// a la otra.
///
/// Por que NO se prueba mandando peticiones de verdad: levantar la tuberia HTTP exige las
/// dos cadenas de conexion y un Postgres vivo, y estas 365 pruebas corren sin base de
/// datos a proposito. Se prueban las dos piezas de las que depende el resultado, que son
/// justo las que alguien puede romper sin darse cuenta:
///
///   1. Lo que el token LLEVA —audiencia y `ambito`—, ejercitando el emisor de verdad.
///   2. Lo que cada controlador EXIGE, por reflexion.
///
/// Lo que queda fuera y hay que decirlo: que ASP.NET aplique bien esas policies. Eso es
/// del framework, y comprobarlo seria probar a Microsoft.
/// </summary>
public class AmbitoCruzadoPruebas
{
    private static readonly Assembly Api = typeof(OrigenesPermitidos).Assembly;

    /// <summary>La llave no es secreta aqui: solo tiene que pasar el minimo de 32 bytes.</summary>
    private static ProveedorTokensJwt Proveedor(OpcionesJwt? opciones = null)
        => new(Options.Create(opciones ?? new OpcionesJwt
        {
            Llave = "una-llave-de-pruebas-con-mas-de-32-bytes",
        }));

    private static JsonWebToken Leer(string token) => new(token);

    private static string? Claim(JsonWebToken token, string tipo)
        => token.Claims.FirstOrDefault(c => c.Type == tipo)?.Value;

    // ------------------------------------------------------------------
    // 1. Lo que el token lleva
    // ------------------------------------------------------------------

    [Fact]
    public void El_token_de_plataforma_lleva_su_audiencia_y_su_ambito()
    {
        var opciones = new OpcionesJwt { Llave = "una-llave-de-pruebas-con-mas-de-32-bytes" };

        var emitido = Proveedor(opciones)
            .EmitirDePlataforma(Guid.CreateVersion7(), "super@maqvia.com", "Super");

        var token = Leer(emitido.Token);

        Assert.Equal(opciones.AudienciaPlataforma, Assert.Single(token.Audiences));
        Assert.Equal(ProveedorTokensJwt.AmbitoPlataforma, Claim(token, ProveedorTokensJwt.ClaimAmbito));
    }

    [Fact]
    public void El_token_de_empresa_lleva_su_audiencia_y_su_ambito()
    {
        var opciones = new OpcionesJwt { Llave = "una-llave-de-pruebas-con-mas-de-32-bytes" };

        var emitido = Proveedor(opciones).EmitirDeEmpresa(
            Guid.CreateVersion7(), "operador@bajio.mx", "Operador",
            Guid.CreateVersion7(), "bajio", accesoTotal: false,
            permisos: ["equipos.ver"], roles: ["operador"]);

        var token = Leer(emitido.Token);

        Assert.Equal(opciones.AudienciaEmpresa, Assert.Single(token.Audiences));
        Assert.Equal(ProveedorTokensJwt.AmbitoEmpresa, Claim(token, ProveedorTokensJwt.ClaimAmbito));
    }

    /// <summary>
    /// NINGUNO LLEVA EL AMBITO DEL OTRO. Es la forma directa de decir que un token no puede
    /// satisfacer la policy contraria: las dos son `RequireClaim(ambito, ...)`.
    /// </summary>
    [Fact]
    public void Ningun_token_lleva_el_ambito_del_otro()
    {
        var proveedor = Proveedor();

        var plataforma = Leer(
            proveedor.EmitirDePlataforma(Guid.CreateVersion7(), "super@maqvia.com", "Super").Token);

        var empresa = Leer(proveedor.EmitirDeEmpresa(
            Guid.CreateVersion7(), "operador@bajio.mx", "Operador",
            Guid.CreateVersion7(), "bajio", accesoTotal: true,
            permisos: [], roles: ["administrador"]).Token);

        Assert.NotEqual(ProveedorTokensJwt.AmbitoEmpresa, Claim(plataforma, ProveedorTokensJwt.ClaimAmbito));
        Assert.NotEqual(ProveedorTokensJwt.AmbitoPlataforma, Claim(empresa, ProveedorTokensJwt.ClaimAmbito));
    }

    /// <summary>
    /// EL TOKEN DE PLATAFORMA NO LLEVA TENANT, y eso es la otra mitad: aunque alguien le
    /// aflojara la policy a un endpoint de empresa, `MiddlewareTenant` corta la peticion —un
    /// ambito de plataforma con claim de tenant se rechaza— y sin tenant resuelto el
    /// `ContextoEmpresa` revienta antes de abrir ninguna base.
    /// </summary>
    [Fact]
    public void El_token_de_plataforma_NO_lleva_tenant_ni_permisos()
    {
        var token = Leer(
            Proveedor().EmitirDePlataforma(Guid.CreateVersion7(), "super@maqvia.com", "Super").Token);

        Assert.Null(Claim(token, ProveedorTokensJwt.ClaimTenant));
        Assert.Null(Claim(token, ProveedorTokensJwt.ClaimEmpresa));
        Assert.Null(Claim(token, ProveedorTokensJwt.ClaimPermisos));
        Assert.Null(Claim(token, ProveedorTokensJwt.ClaimAccesoTotal));
        Assert.Null(Claim(token, ProveedorTokensJwt.ClaimRoles));
    }

    /// <summary>
    /// LAS DOS AUDIENCIAS POR OMISION SON DISTINTAS.
    ///
    /// Parece de perogrullo y no lo es: las dos salen de configuracion, asi que un
    /// `Jwt__AudienciaEmpresa` mal puesto en Railway las igualaria, y con eso el
    /// `ValidateAudience` del arranque deja de separar nada. Seguiria quedando el claim
    /// `ambito`, pero la primera de las dos barreras se habria caido en silencio.
    /// </summary>
    [Fact]
    public void Las_dos_audiencias_por_omision_no_coinciden()
    {
        var opciones = new OpcionesJwt();

        Assert.NotEqual(opciones.AudienciaPlataforma, opciones.AudienciaEmpresa);
    }

    // ------------------------------------------------------------------
    // 2. Lo que cada controlador exige
    // ------------------------------------------------------------------

    /// <summary>
    /// Los controladores que NO exigen ambito en la clase, con su razon. Es una lista
    /// explicita y corta a proposito: agregar uno obliga a escribirlo aqui —o sea a
    /// justificarlo en una revision— en lugar de que un controlador sin ambito pase
    /// inadvertido. Mismo criterio que la lista de `PermisosDeclaradosPruebas`.
    /// </summary>
    private static readonly HashSet<string> SinAmbitoEnLaClase = new(StringComparer.Ordinal)
    {
        // Los tres flujos anonimos por empresa: se entra sin token, que es justo el punto.
        "SesionEmpresaController",
        "InvitacionesController",
        "RestablecimientosController",

        // El login de plataforma. Su accion `actual` SI exige ambito, y eso lo comprueba
        // la prueba de abajo.
        "SesionController",
    };

    public static TheoryData<Type> Controladores()
    {
        var datos = new TheoryData<Type>();

        foreach (var tipo in Api.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t)))
        {
            datos.Add(tipo);
        }

        return datos;
    }

    /// <summary>
    /// TODO CONTROLADOR PINTA SU AMBITO, y el que le toca por donde vive: los de
    /// `Controladores/Plataforma` exigen plataforma, y el resto, empresa.
    ///
    /// Esta es la prueba que faltaba. Quitarle el `[Authorize(...)]` a un controlador de
    /// empresa lo dejaba abierto a un token de plataforma y la suite seguia en verde.
    /// </summary>
    [Theory]
    [MemberData(nameof(Controladores))]
    public void Cada_controlador_exige_el_ambito_que_le_toca(Type controlador)
    {
        if (SinAmbitoEnLaClase.Contains(controlador.Name))
        {
            return;
        }

        var politicas = controlador.GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        var esperada = controlador.Namespace?.Contains(".Plataforma", StringComparison.Ordinal) == true
            ? PoliticasAutorizacion.Plataforma
            : PoliticasAutorizacion.Empresa;

        Assert.True(
            politicas.Contains(esperada, StringComparer.Ordinal),
            $"{controlador.Name} deberia exigir la policy '{esperada}' en la clase. "
            + $"Declara: {(politicas.Count == 0 ? "ninguna" : string.Join(", ", politicas))}. "
            + "Si es un flujo anonimo a proposito, agregalo a SinAmbitoEnLaClase con su razon.");
    }

    /// <summary>
    /// NINGUNO EXIGE LOS DOS. Un controlador con las dos policies no es mas estricto: son
    /// dos requisitos que ningun token puede cumplir a la vez, asi que responderia 403 a
    /// todo el mundo — un endpoint muerto que nadie nota hasta que alguien lo necesita.
    /// </summary>
    [Theory]
    [MemberData(nameof(Controladores))]
    public void Ningun_controlador_exige_los_dos_ambitos(Type controlador)
    {
        var politicas = controlador.GetCustomAttributes<AuthorizeAttribute>()
            .Select(a => a.Policy)
            .ToList();

        Assert.False(
            politicas.Contains(PoliticasAutorizacion.Plataforma, StringComparer.Ordinal)
            && politicas.Contains(PoliticasAutorizacion.Empresa, StringComparer.Ordinal),
            $"{controlador.Name} exige los dos ambitos a la vez: ningun token cumple los dos.");
    }

    /// <summary>
    /// La accion `sesion/actual` del login de plataforma exige ambito, aunque su clase no.
    ///
    /// Va aparte porque `SesionController` esta en la lista de exentos —su POST es anonimo—
    /// y sin esto la exencion taparia tambien la accion que si tiene que estar protegida.
    /// </summary>
    [Fact]
    public void La_sesion_actual_de_plataforma_exige_ambito_de_plataforma()
    {
        var accion = Api.GetTypes()
            .Single(t => t.Name == "SesionController")
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(m => m.GetCustomAttributes<HttpGetAttribute>().Any());

        var politicas = accion.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy);

        Assert.Contains(PoliticasAutorizacion.Plataforma, politicas);
    }
}
