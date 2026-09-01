using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Dominio.Seguridad;
using Microsoft.Extensions.Logging.Abstractions;
using Usuario = Maquinaria.Dominio.Seguridad.Usuario;

namespace Maquinaria.Api.Tests.Empresas;

/// <summary>
/// CUANDO SE DICE «TU SERVICIO ESTA SUSPENDIDO», Y CUANDO NO.
///
/// El mensaje uniforme del login —«Empresa, correo o contrasena incorrectos»— existe para
/// que nadie pueda averiguar que slugs son clientes probandolos uno por uno. Decirle a
/// cualquiera que una empresa esta suspendida deshace eso: confirma que existe.
///
/// La salida es el ORDEN: se comprueban las credenciales PRIMERO y solo despues se explica
/// la suspension. Quien acerto correo y contrasena de esa empresa ya sabia que existe, asi
/// que decirselo no le regala nada; a quien solo prueba slugs le sigue contestando el
/// mensaje de siempre.
///
/// ESTE ARCHIVO FIJA ESE ORDEN. Si alguien mueve la comprobacion de `PuedeOperar` antes de
/// verificar la contrasena —que es donde «naturalmente» apetece ponerla, y donde estuvo
/// hasta el 2026-09-01 por via del middleware— la segunda prueba falla.
/// </summary>
public class SuspensionEnElLoginPruebas
{
    private const string Correo = "ana@bajio.mx";
    private const string Contrasena = "una-contrasena-larga";

    [Fact]
    public async Task Con_credenciales_correctas_y_empresa_suspendida_SE_DICE()
    {
        var caso = Armar(EstadoTenant.Suspendido);

        var resultado = await Ejecutar(caso, Contrasena);

        Assert.Null(resultado.Sesion);
        Assert.Equal(EstadoTenant.Suspendido, resultado.ServicioDetenido);
    }

    /// <summary>
    /// LA PRUEBA QUE PROTEGE LA LISTA DE CLIENTES. Sin credenciales validas, una empresa
    /// suspendida es indistinguible de una que no existe.
    /// </summary>
    [Fact]
    public async Task Con_la_contrasena_MAL_y_empresa_suspendida_NO_se_dice()
    {
        var caso = Armar(EstadoTenant.Suspendido);

        var resultado = await Ejecutar(caso, "otra-cosa");

        Assert.Null(resultado.Sesion);
        Assert.Null(resultado.ServicioDetenido);
    }

    [Fact]
    public async Task Con_un_correo_que_no_existe_tampoco_se_dice()
    {
        var caso = Armar(EstadoTenant.Suspendido);

        var resultado = await caso.EjecutarAsync(
            "bajio",
            new PeticionSesionEmpresa("nadie@bajio.mx", Contrasena),
            null, null, default);

        Assert.Null(resultado.ServicioDetenido);
    }

    [Fact]
    public async Task Cancelada_se_distingue_de_suspendida()
    {
        var caso = Armar(EstadoTenant.Cancelado);

        var resultado = await Ejecutar(caso, Contrasena);

        // El controlador redacta un texto distinto para cada uno, asi que el estado tiene
        // que llegarle, no un booleano.
        Assert.Equal(EstadoTenant.Cancelado, resultado.ServicioDetenido);
    }

    [Theory]
    [InlineData(EstadoTenant.Prueba)]
    [InlineData(EstadoTenant.Activo)]
    public async Task Una_empresa_que_opera_entra_normal(EstadoTenant estado)
    {
        var caso = Armar(estado);

        var resultado = await Ejecutar(caso, Contrasena);

        Assert.Null(resultado.ServicioDetenido);
        Assert.NotNull(resultado.Sesion);
    }

    /// <summary>
    /// Una empresa cuya base todavia no esta lista NO se resuelve —lo decide el middleware
    /// con `BaseDisponible`— asi que el caso de uso ni llega a mirarla. Se comprueba con el
    /// tenant sin resolver, que es como le llega.
    /// </summary>
    [Fact]
    public async Task Sin_tenant_resuelto_se_rechaza_sin_explicar_nada()
    {
        var caso = Armar(EstadoTenant.Activo, resuelto: false);

        var resultado = await Ejecutar(caso, Contrasena);

        Assert.Null(resultado.Sesion);
        Assert.Null(resultado.ServicioDetenido);
    }

    // ------------------------------------------------------------------ armado --

    private static Task<ResultadoSesionEmpresa> Ejecutar(
        IniciarSesionEmpresa caso, string contrasena)
        => caso.EjecutarAsync(
            "bajio", new PeticionSesionEmpresa(Correo, contrasena), null, null, default);

    private static IniciarSesionEmpresa Armar(EstadoTenant estado, bool resuelto = true)
    {
        var usuarios = new UsuariosFalsos();

        return new IniciarSesionEmpresa(
            new TenantFalso(estado, resuelto),
            () => usuarios,
            new HashFalso(),
            new GeneradorFalso(),
            new ProveedorFalso(),
            NullLogger<IniciarSesionEmpresa>.Instance);
    }

    private sealed class TenantFalso(EstadoTenant estado, bool resuelto) : IContextoTenant
    {
        public bool EstaResuelto => resuelto;

        public TenantResuelto Actual => resuelto
            ? new TenantResuelto(
                Guid.CreateVersion7(), "bajio", "maquinaria_bajio", "Maquinaria del Bajio",
                estado, EstadoAprovisionamiento.Lista, "America/Mexico_City", "MXN",
                new HashSet<string> { "equipos" }, new Dictionary<string, int>())
            : throw new InvalidOperationException("No hay tenant resuelto.");

        public void Establecer(TenantResuelto tenant) => throw new NotSupportedException();
    }

    /// <summary>Hash de mentira, comparacion de verdad: lo que importa es que distinga.</summary>
    private sealed class HashFalso : IHashContrasenas
    {
        public string Hash(string contrasena) => $"hash:{contrasena}";

        public ResultadoVerificacion Verificar(string hashAlmacenado, string contrasena)
            => new(hashAlmacenado == $"hash:{contrasena}", false);

        public void VerificarSenuelo(string contrasena)
        {
        }
    }

    private sealed class GeneradorFalso : IGeneradorTokens
    {
        public TokenGenerado Generar() => new("en-claro", "su-hash");

        public string Hashear(string enClaro) => $"hash:{enClaro}";
    }

    private sealed class ProveedorFalso : IProveedorTokens
    {
        public TokenEmitido EmitirDeEmpresa(
            Guid usuarioId, string correo, string nombre, Guid tenantId, string slug,
            bool accesoTotal, IReadOnlyList<string> permisos, IReadOnlyList<string> roles)
            => new($"jwt-de-{correo}", DateTime.UtcNow.AddMinutes(15));

        public TokenEmitido EmitirDePlataforma(Guid usuarioId, string correo, string nombre)
            => throw new NotSupportedException();
    }

    private sealed class UsuariosFalsos : IUsuariosEmpresa
    {
        private readonly List<Usuario> _usuarios =
        [
            new()
            {
                Correo = Correo,
                Nombre = "Ana",
                Estado = EstadoUsuario.Activo,
                HashContrasena = $"hash:{Contrasena}",
            },
        ];

        public Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken ct)
            => Task.FromResult(_usuarios.FirstOrDefault(u => u.Correo == correo));

        public Task<IReadOnlyList<RolEfectivo>> RolesDeAsync(Guid usuarioId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RolEfectivo>>([new RolEfectivo("operador", false)]);

        public Task<IReadOnlyList<string>> PermisosDeAsync(Guid usuarioId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(["equipos.consultar"]);

        public Task RegistrarAccesoAsync(
            Guid usuarioId, DateTime cuandoUtc, string? hashNuevo, CancellationToken ct)
            => Task.CompletedTask;

        public Task CrearSesionAsync(SesionRefresh sesion, CancellationToken ct)
            => Task.CompletedTask;

        // Lo que el login no toca. Lanzan a proposito: si alguna empieza a llamarse desde
        // este camino, es un cambio de comportamiento que hay que mirar, no algo que deba
        // pasar en silencio.
        public Task<TokenConUsuario?> BuscarTokenVigenteAsync(
            string hashToken, PropositoToken proposito, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Usuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<SesionRefresh?> BuscarSesionPorHashAsync(string hashToken, CancellationToken ct)
            => throw new NotSupportedException();

        public Task RotarSesionAsync(Guid anteriorId, SesionRefresh nueva, CancellationToken ct)
            => throw new NotSupportedException();

        public Task RevocarSesionesDeAsync(Guid usuarioId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task AceptarInvitacionAsync(
            Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct)
            => throw new NotSupportedException();

        public Task RestablecerContrasenaAsync(
            Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct)
            => throw new NotSupportedException();

        public Task EmitirTokenAsync(
            Guid usuarioId, PropositoToken proposito, string hashToken, DateTime expiraEn,
            CancellationToken ct)
            => throw new NotSupportedException();
    }
}
