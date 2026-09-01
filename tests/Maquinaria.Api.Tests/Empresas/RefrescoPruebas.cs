using System.Diagnostics;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Dominio.Seguridad;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.Extensions.Logging.Abstractions;

// Igual que en RestablecimientoPruebas: Usuario es homonimo a proposito y aqui se
// necesitan los dos namespaces, el de plataforma por los estados del tenant.
using Usuario = Maquinaria.Dominio.Seguridad.Usuario;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El refresco ROTATIVO de la sesion de empresa. Se prueba sin base de datos, y las dos
/// piezas que no se falsifican son las que importan: el generador de tokens es el real
/// —asi que los hashes son de verdad SHA-256— y el repositorio falso reimplementa
/// exactamente lo que hace UsuariosEmpresaEf, incluido no filtrar las sesiones
/// reemplazadas al buscarlas, que es lo que permite detectar el reuso.
///
/// El punto de partida de casi todas es un LOGIN de verdad, no una sesion sembrada a
/// mano: lo que se quiere comprobar es que el token que entrega el login sirve para
/// refrescar y que despues de rotar ya no sirve.
/// </summary>
public class RefrescoPruebas
{
    private const string Correo = "ana@bajio.mx";
    private const string Contrasena = "Contrasena-Larga-Y-Buena-2026";

    [Fact]
    public async Task Un_token_valido_rota_la_sesion()
    {
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        var refrescada = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        Assert.NotNull(refrescada);

        // Rotativo significa DOS cosas, y las dos se comprueban aqui: el token cambia...
        Assert.NotEqual(login.TokenRefresco, refrescada!.Value.TokenRefresco);

        // ...y el canjeado queda revocado y enlazado con su sucesor. Sin el enlace no hay
        // deteccion de reuso; sin la revocacion, los dos tokens servirian.
        var anterior = repo.Sesiones[0];
        var nueva = repo.Sesiones[1];

        Assert.Equal(2, repo.Sesiones.Count);
        Assert.NotNull(anterior.RevocadoEn);
        Assert.Equal(nueva.Id, anterior.ReemplazadoPorId);
        Assert.Null(nueva.RevocadoEn);
        Assert.Null(nueva.ReemplazadoPorId);
    }

    [Fact]
    public async Task El_token_canjeado_no_sirve_para_una_segunda_rotacion()
    {
        var (caso, _, _) = Armar();
        var login = await Entrar(caso);

        var primera = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);
        var segunda = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        Assert.NotNull(primera);
        Assert.Null(segunda);
    }

    [Fact]
    public async Task Un_token_reusado_revoca_toda_la_cadena()
    {
        // LA DEFENSA CENTRAL DE LA ROTACION. Un token ya canjeado solo puede llegar de una
        // copia: el cliente legitimo ya cambio al sucesor. No se puede saber quien de los
        // dos es el ladron, asi que se corta la sesion de los dos y se obliga a entrar de
        // nuevo con contrasena, que es lo unico que el ladron no tiene.
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        var legitima = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        // Llega el token viejo: robado.
        var reuso = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        Assert.Null(reuso);
        Assert.Equal(repo.Usuarios[0].Id, Assert.Single(repo.SesionesRevocadasDe));

        // Y no basta con que se registre la revocacion: el token que estaba vivo —el que
        // tiene el cliente legitimo, o el ladron— tampoco sirve ya.
        var despues = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(legitima!.Value.TokenRefresco), null, null, default);

        Assert.Null(despues);
        Assert.All(repo.Sesiones, s => Assert.NotNull(s.RevocadoEn));
    }

    [Fact]
    public async Task Un_token_caducado_se_rechaza()
    {
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        // Treinta dias y un minuto despues.
        repo.Sesiones[0].ExpiraEn = DateTime.UtcNow.AddMinutes(-1);

        var refrescada = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        Assert.Null(refrescada);

        // Un caducado NO dispara la revocacion en cadena: no es senal de robo, es una
        // sesion que llego al final de su vida.
        Assert.Empty(repo.SesionesRevocadasDe);
    }

    [Fact]
    public async Task Un_token_revocado_se_rechaza()
    {
        // Es el caso del restablecimiento de contrasena, que revoca todas las sesiones.
        // Si esto no se comprobara, cambiar la contrasena no cerraria nada.
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        await repo.RevocarSesionesDeAsync(repo.Usuarios[0].Id, default);

        var refrescada = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        Assert.Null(refrescada);
    }

    [Theory]
    [InlineData("token-que-nunca-existio")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Un_token_inexistente_o_vacio_se_rechaza(string token)
    {
        var (caso, _, _) = Armar();

        Assert.Null(await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(token), null, null, default));
    }

    [Fact]
    public async Task Una_empresa_que_no_se_pudo_resolver_se_rechaza()
    {
        // Sin tenant no hay base donde buscar la sesion. Cubre la empresa inexistente, la
        // suspendida y la que todavia se esta aprovisionando, todas por la misma puerta.
        var (caso, _, _) = Armar(empresaExiste: false);

        Assert.Null(await caso.RefrescarAsync(
            "no-existe", new PeticionRefresco("cualquier-cosa"), null, null, default));
    }

    [Fact]
    public async Task Todos_los_rechazos_tardan_lo_mismo()
    {
        // Sin el piso, el rechazo de una empresa inexistente no toca ninguna base y el de
        // un token inexistente paga una consulta. Como el endpoint es ANONIMO, esa
        // diferencia lo convierte en un enumerador de slugs de clientes — justo lo que el
        // login y el restablecimiento se cuidan de no ser, y lo que el CORS por subdominio
        // acepta a proposito para no serlo tampoco.
        var (conEmpresa, _, _) = Armar();
        var (sinEmpresa, _, _) = Armar(empresaExiste: false);

        var tardaCon = await Cronometrar(() => conEmpresa.RefrescarAsync(
            "bajio", new PeticionRefresco("token-que-nunca-existio"), null, null, default));
        var tardaSin = await Cronometrar(() => sinEmpresa.RefrescarAsync(
            "no-existe", new PeticionRefresco("token-que-nunca-existio"), null, null, default));

        Assert.True(
            tardaCon >= IniciarSesionEmpresa.PisoDeRechazoRefresco - Tolerancia,
            $"El rechazo con empresa respondio en {tardaCon}, por debajo del piso.");
        Assert.True(
            tardaSin >= IniciarSesionEmpresa.PisoDeRechazoRefresco - Tolerancia,
            $"El rechazo sin empresa respondio en {tardaSin}, por debajo del piso.");
        Assert.True(
            (tardaCon - tardaSin).Duration() < Tolerancia,
            $"Diferencia observable entre los dos caminos: {tardaCon} contra {tardaSin}.");
    }

    [Fact]
    public async Task El_camino_correcto_no_paga_el_piso()
    {
        // El piso es de los RECHAZOS. Si se aplicara siempre, el interceptor del frontend
        // arrastraria 400 ms en cada renovacion, y el tiempo de un refresco exitoso no es
        // informacion: quien lo consigue ya tenia un token valido de esa empresa.
        var (caso, _, _) = Armar();
        var login = await Entrar(caso);

        var tarda = await Cronometrar(() => caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default));

        Assert.True(
            tarda < IniciarSesionEmpresa.PisoDeRechazoRefresco,
            $"El refresco correcto tardo {tarda}.");
    }

    [Theory]
    [InlineData(EstadoUsuario.Suspendido)]
    [InlineData(EstadoUsuario.Baja)]
    public async Task Quien_ya_no_esta_activo_no_refresca_y_pierde_sus_sesiones(EstadoUsuario estado)
    {
        // El estado se comprueba en CADA refresco. Sin esto, suspender a alguien tardaria
        // hasta 30 dias en surtir efecto, porque su cadena de refresco seguiria emitiendo
        // tokens de acceso nuevos cada 15 minutos.
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        repo.Usuarios[0].Estado = estado;

        var refrescada = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        Assert.Null(refrescada);
        Assert.Equal(repo.Usuarios[0].Id, Assert.Single(repo.SesionesRevocadasDe));
    }

    [Fact]
    public async Task El_refresco_vuelve_a_resolver_los_permisos()
    {
        // Los permisos viajan DENTRO del token, asi que el refresco es el unico momento en
        // que un cambio de rol puede entrar. Si copiara los del token anterior, revocar un
        // permiso no surtiria efecto nunca mientras la persona siguiera refrescando.
        var (caso, repo, proveedor) = Armar();
        var login = await Entrar(caso);

        Assert.Equal(["equipos.consultar", "equipos.editar"], login.Permisos);

        repo.PermisosDelRol = ["equipos.consultar"];

        var refrescada = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        Assert.Equal(["equipos.consultar"], refrescada!.Value.Permisos);
        Assert.Equal(["equipos.consultar"], proveedor.UltimosPermisos);
    }

    [Fact]
    public async Task La_compuerta_de_modulos_tambien_aplica_al_refrescar()
    {
        // permisos del rol INTERSECCION modulos del plan. Es la parte que un refresco
        // escrito aparte olvidaria, y olvidarla significa entregar permisos sobre modulos
        // que la empresa no contrato.
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        repo.PermisosDelRol = ["equipos.consultar", "logistica.crear"];

        var refrescada = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        // 'logistica' no esta en el plan del tenant falso.
        Assert.Equal(["equipos.consultar"], refrescada!.Value.Permisos);
    }

    [Fact]
    public async Task El_refresco_devuelve_la_misma_forma_que_el_login()
    {
        // El contrato que consume el interceptor del frontend. Si el refresco devolviera
        // otra forma, el cliente tendria que aprender dos y traducir entre ellas.
        var (caso, _, _) = Armar();
        var login = await Entrar(caso);

        var refrescada = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        var sesion = refrescada!.Value;

        Assert.Equal(login.Nombre, sesion.Nombre);
        Assert.Equal(login.Correo, sesion.Correo);
        Assert.Equal(login.Empresa, sesion.Empresa);
        Assert.Equal(login.AccesoTotal, sesion.AccesoTotal);
        Assert.Equal(login.Permisos, sesion.Permisos);
        Assert.NotEmpty(sesion.Token);
        Assert.NotEmpty(sesion.TokenRefresco);
        Assert.True(sesion.ExpiraEn > DateTime.UtcNow);
    }

    [Fact]
    public async Task El_token_de_refresco_nunca_se_guarda_en_claro()
    {
        // Mismo criterio que las contrasenas y que token_acceso: leer la base no debe dar
        // sesiones usables. Y de paso fija que lo guardado es el hash del token entregado,
        // no otra cosa que casualmente valide.
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        var refrescada = await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        var enClaro = new[] { login.TokenRefresco, refrescada!.Value.TokenRefresco };

        Assert.All(repo.Sesiones, s => Assert.DoesNotContain(s.HashToken, enClaro));
        Assert.Equal(repo.Generador.Hashear(login.TokenRefresco), repo.Sesiones[0].HashToken);
        Assert.Equal(
            repo.Generador.Hashear(refrescada.Value.TokenRefresco), repo.Sesiones[1].HashToken);
    }

    [Fact]
    public async Task La_rotacion_no_alarga_la_vida_de_la_cadena_mas_de_lo_debido()
    {
        // Cada rotacion emite una vigencia nueva desde hoy, asi que una sesion viva puede
        // durar indefinidamente mientras se use — eso es lo esperado de un refresco
        // rotativo—. Lo que se fija aqui es que la vigencia sea la MISMA que la del login
        // y no un valor distinto por camino.
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), null, null, default);

        var vigencia = repo.Sesiones[1].ExpiraEn - DateTime.UtcNow;

        Assert.InRange(
            vigencia,
            TimeSpan.FromDays(IniciarSesionEmpresa.DiasVigenciaRefresco) - TimeSpan.FromMinutes(1),
            TimeSpan.FromDays(IniciarSesionEmpresa.DiasVigenciaRefresco));
    }

    [Fact]
    public async Task El_refresco_registra_la_ip_y_el_agente_de_la_peticion_nueva()
    {
        // Sirven para mostrarle al usuario sus sesiones activas y cerrarlas. Copiar los de
        // la sesion anterior mostraria la maquina de la que se entro hace un mes.
        var (caso, repo, _) = Armar();
        var login = await Entrar(caso);

        await caso.RefrescarAsync(
            "bajio", new PeticionRefresco(login.TokenRefresco), "10.0.0.7", "Firefox", default);

        Assert.Equal("10.0.0.7", repo.Sesiones[1].Ip?.ToString());
        Assert.Equal("Firefox", repo.Sesiones[1].AgenteUsuario);
    }

    // ------------------------------------------------------------- andamio --

    /// <summary>Igual que en RestablecimientoPruebas: holgada para no parpadear.</summary>
    private static readonly TimeSpan Tolerancia = TimeSpan.FromMilliseconds(250);

    private static async Task<TimeSpan> Cronometrar(Func<Task<SesionEmpresa?>> accion)
    {
        var reloj = Stopwatch.StartNew();
        await accion();
        reloj.Stop();

        return reloj.Elapsed;
    }

    private static async Task<SesionEmpresa> Entrar(IniciarSesionEmpresa caso)
    {
        var sesion = await caso.EjecutarAsync(
            "bajio", new PeticionSesionEmpresa(Correo, Contrasena), null, null, default);

        Assert.NotNull(sesion);

        return sesion!.Value;
    }

    private static (IniciarSesionEmpresa Caso, UsuariosFalsos Repo, ProveedorFalso Proveedor) Armar(
        bool empresaExiste = true)
    {
        var repo = new UsuariosFalsos();
        var proveedor = new ProveedorFalso();

        var caso = new IniciarSesionEmpresa(
            new TenantFalso(empresaExiste),
            () => repo,
            new HashFalso(),
            repo.Generador,
            proveedor,
            NullLogger<IniciarSesionEmpresa>.Instance);

        return (caso, repo, proveedor);
    }

    /// <summary>
    /// Reimplementa lo que hace UsuariosEmpresaEf para las sesiones, con la parte que mas
    /// importa copiada tal cual: BuscarSesionPorHashAsync NO filtra por revocada ni por
    /// reemplazada. Si filtrara, el reuso de un token robado se veria como un token
    /// inexistente y la revocacion en cadena nunca se disparara.
    /// </summary>
    private sealed class UsuariosFalsos : IUsuariosEmpresa
    {
        public readonly List<Usuario> Usuarios =
        [
            new()
            {
                Correo = Correo,
                Nombre = "Ana",
                Estado = EstadoUsuario.Activo,
                HashContrasena = $"hash:{Contrasena}",
            },
        ];

        public readonly List<SesionRefresh> Sesiones = [];

        public readonly List<Guid> SesionesRevocadasDe = [];

        public IReadOnlyList<string> PermisosDelRol { get; set; } =
            ["equipos.editar", "equipos.consultar"];

        public bool AccesoTotal { get; set; }

        /// <summary>El generador real: los tokens y sus hashes son de verdad.</summary>
        public readonly IGeneradorTokens Generador = new GeneradorTokensAleatorios();

        public Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken ct)
            => Task.FromResult(Usuarios.FirstOrDefault(u => u.Correo == correo));

        public Task<Usuario?> BuscarPorIdAsync(Guid usuarioId, CancellationToken ct)
            => Task.FromResult(Usuarios.FirstOrDefault(u => u.Id == usuarioId));

        public Task<IReadOnlyList<string>> PermisosDeAsync(Guid usuarioId, CancellationToken ct)
            => Task.FromResult(PermisosDelRol);

        public Task<IReadOnlyList<RolEfectivo>> RolesDeAsync(
            Guid usuarioId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RolEfectivo>>(
                [new RolEfectivo("operador", AccesoTotal)]);

        public Task RegistrarAccesoAsync(
            Guid usuarioId, DateTime cuandoUtc, string? hashNuevo, CancellationToken ct)
        {
            Usuarios.First(u => u.Id == usuarioId).UltimoAccesoEn = cuandoUtc;

            return Task.CompletedTask;
        }

        public Task CrearSesionAsync(SesionRefresh sesion, CancellationToken ct)
        {
            Sesiones.Add(sesion);

            return Task.CompletedTask;
        }

        public Task<SesionRefresh?> BuscarSesionPorHashAsync(
            string hashToken, CancellationToken ct)
            => Task.FromResult(Sesiones.FirstOrDefault(s => s.HashToken == hashToken));

        public Task RotarSesionAsync(Guid anteriorId, SesionRefresh nueva, CancellationToken ct)
        {
            Sesiones.Add(nueva);

            var anterior = Sesiones.First(s => s.Id == anteriorId);

            anterior.ReemplazadoPorId = nueva.Id;
            anterior.RevocadoEn = DateTime.UtcNow;

            return Task.CompletedTask;
        }

        public Task RevocarSesionesDeAsync(Guid usuarioId, CancellationToken ct)
        {
            SesionesRevocadasDe.Add(usuarioId);

            foreach (var viva in Sesiones.Where(s => s.UsuarioId == usuarioId && s.RevocadoEn is null))
            {
                viva.RevocadoEn = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        // ---- lo que este flujo no usa ----
        public Task<TokenConUsuario?> BuscarTokenVigenteAsync(
            string hashToken, PropositoToken proposito, CancellationToken ct)
            => throw new NotSupportedException();

        public Task EmitirTokenAsync(
            Guid usuarioId, PropositoToken proposito, string hashToken, DateTime expiraEn,
            CancellationToken ct)
            => throw new NotSupportedException();

        public Task RestablecerContrasenaAsync(
            Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct)
            => throw new NotSupportedException();

        public Task AceptarInvitacionAsync(
            Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Guarda lo ULTIMO que se le pidio emitir. Un JWT de verdad obligaria a decodificarlo
    /// para comprobar los permisos, y lo que se quiere verificar es que la compuerta
    /// entrego el conjunto correcto, no que la libreria de JWT funciona.
    /// </summary>
    private sealed class ProveedorFalso : IProveedorTokens
    {
        public IReadOnlyList<string> UltimosPermisos { get; private set; } = [];

        public IReadOnlyList<string> UltimosRoles { get; private set; } = [];

        public TokenEmitido EmitirDeEmpresa(
            Guid usuarioId, string correo, string nombre, Guid tenantId, string slug,
            bool accesoTotal, IReadOnlyList<string> permisos, IReadOnlyList<string> roles)
        {
            UltimosPermisos = permisos;
            UltimosRoles = roles;

            return new TokenEmitido($"jwt-de-{correo}", DateTime.UtcNow.AddMinutes(15));
        }

        public TokenEmitido EmitirDePlataforma(Guid usuarioId, string correo, string nombre)
            => throw new NotSupportedException();
    }

    /// <param name="resuelto">
    /// Falso simula las tres cosas que dejan una peticion sin tenant: la empresa no
    /// existe, no puede operar, o el slug no venia en la ruta.
    /// </param>
    private sealed class TenantFalso(bool resuelto) : IContextoTenant
    {
        public bool EstaResuelto => resuelto;

        public TenantResuelto Actual => resuelto
            ? new TenantResuelto(
                Guid.CreateVersion7(), "bajio", "maquinaria_bajio",
                "Maquinaria del Bajio SA de CV",
                EstadoTenant.Activo, EstadoAprovisionamiento.Lista,
                "America/Mexico_City", "MXN",

                // El plan incluye equipos y NO incluye logistica: es lo que hace visible
                // la compuerta en las pruebas.
                new HashSet<string> { "equipos", "rentas" },
                new Dictionary<string, int>())
            : throw new InvalidOperationException("No hay tenant resuelto.");

        public void Establecer(TenantResuelto tenant) => throw new NotSupportedException();
    }

    /// <summary>Sin PBKDF2: 600 mil iteraciones falsearian las mediciones de tiempo.</summary>
    private sealed class HashFalso : IHashContrasenas
    {
        public string Hash(string contrasena) => $"hash:{contrasena}";

        public ResultadoVerificacion Verificar(string hashAlmacenado, string contrasena)
            => new(hashAlmacenado == $"hash:{contrasena}", false);

        public void VerificarSenuelo(string contrasena)
        {
        }
    }
}
