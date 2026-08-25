using System.Diagnostics;
using Maquinaria.Aplicacion.Correo;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Dominio.Seguridad;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.Extensions.Logging.Abstractions;

// Usuario es homonimo a proposito —hay uno de plataforma y uno de empresa, la misma idea
// en dos mundos separados fisicamente— y aqui se necesitan los dos namespaces: el de
// plataforma por los estados del tenant. El alias fija cual es cual.
using Usuario = Maquinaria.Dominio.Seguridad.Usuario;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El restablecimiento de contrasena. Se prueba sin base de datos: la regla de vigencia
/// vive en TokenAcceso.Vigente como Expression, asi que la misma condicion que EF Core
/// traduce a SQL se puede compilar y ejecutar aqui. Si alguien la relaja en la consulta,
/// estas pruebas se caen.
/// </summary>
public class VigenciaDeTokenPruebas
{
    private static TokenAcceso Token(
        PropositoToken proposito = PropositoToken.RestablecerContrasena,
        int minutosParaCaducar = 60,
        bool usado = false,
        bool invalidado = false)
        => new()
        {
            UsuarioId = Guid.CreateVersion7(),
            Proposito = proposito,
            HashToken = "da-igual",
            ExpiraEn = DateTime.UtcNow.AddMinutes(minutosParaCaducar),
            UsadoEn = usado ? DateTime.UtcNow.AddMinutes(-1) : null,
            InvalidadoEn = invalidado ? DateTime.UtcNow.AddMinutes(-1) : null,
        };

    private static bool Sirve(TokenAcceso token, PropositoToken para)
        => TokenAcceso.Vigente(para, DateTime.UtcNow).Compile()(token);

    [Theory]
    [InlineData(PropositoToken.Invitacion, PropositoToken.RestablecerContrasena, false)]
    [InlineData(PropositoToken.RestablecerContrasena, PropositoToken.Invitacion, false)]
    [InlineData(PropositoToken.RestablecerContrasena, PropositoToken.RestablecerContrasena, true)]
    [InlineData(PropositoToken.Invitacion, PropositoToken.Invitacion, true)]
    public void Un_token_solo_sirve_para_su_proposito(
        PropositoToken emitidoPara, PropositoToken usadoPara, bool esperado)
    {
        // La tabla token_acceso sirve a los dos flujos. Sin el filtro por proposito, la
        // liga de invitacion de un usuario nuevo cambiaria la contrasena de una cuenta
        // activa, y la de restablecimiento activaria una cuenta invitada saltandose el
        // alta. Es el defecto que produce copiar el flujo de invitacion sin cambiar el
        // enum.
        Assert.Equal(esperado, Sirve(Token(proposito: emitidoPara), usadoPara));
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(-60, false)]
    [InlineData(1, true)]
    public void Un_token_caducado_no_sirve(int minutosParaCaducar, bool esperado)
    {
        // Un restablecimiento dura una hora justamente para acotar la ventana en que un
        // correo interceptado abre la cuenta. Si la comprobacion se hiciera con >= o se
        // olvidara, esa ventana seria infinita.
        Assert.Equal(
            esperado,
            Sirve(Token(minutosParaCaducar: minutosParaCaducar), PropositoToken.RestablecerContrasena));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Un_token_usado_o_invalidado_no_sirve(bool usado, bool invalidado)
    {
        // Usado: lo vuelve de un solo uso, que es lo que impide que la liga que quedo en
        // la bandeja de entrada sirva otra vez dentro de la hora.
        // Invalidado: es como se cancela la liga anterior al pedir otra; si siguiera
        // sirviendo, quedarian dos ligas validas circulando.
        Assert.False(
            Sirve(Token(usado: usado, invalidado: invalidado), PropositoToken.RestablecerContrasena));
    }
}

public class RestablecimientoPruebas
{
    private const string ContrasenaBuena = "Contrasena-Larga-Y-Buena-2026";
    private const string CorreoConCuenta = "ana@bajio.mx";

    // ------------------------------------------------------------ consumir --

    [Fact]
    public async Task Un_token_de_invitacion_no_restablece_la_contrasena()
    {
        var (caso, repo) = ArmarConsumo();
        var token = repo.Emitir(PropositoToken.Invitacion);

        var resultado = await caso.RestablecerAsync("bajio", token, ContrasenaBuena, default);

        Assert.False(resultado.Correcto);
        Assert.Null(repo.Usuarios[0].HashContrasena);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-120)]
    public async Task Un_token_caducado_no_restablece_la_contrasena(int minutos)
    {
        var (caso, repo) = ArmarConsumo();
        var token = repo.Emitir(minutosParaCaducar: minutos);

        var resultado = await caso.RestablecerAsync("bajio", token, ContrasenaBuena, default);

        Assert.False(resultado.Correcto);
        Assert.Null(repo.Usuarios[0].HashContrasena);
    }

    [Fact]
    public async Task Un_token_ya_usado_no_restablece_la_contrasena()
    {
        // Es lo que impide que la liga que se quedo en la bandeja de entrada sirva una
        // segunda vez: quien la vea despues no puede volver a entrar con ella.
        var (caso, repo) = ArmarConsumo();
        var token = repo.Emitir();

        var primera = await caso.RestablecerAsync("bajio", token, ContrasenaBuena, default);
        var segunda = await caso.RestablecerAsync("bajio", token, "Otra-Contrasena-2026", default);

        Assert.True(primera.Correcto);
        Assert.False(segunda.Correcto);
        Assert.Equal($"hash:{ContrasenaBuena}", repo.Usuarios[0].HashContrasena);
    }

    [Fact]
    public async Task Los_tres_rechazos_dicen_exactamente_lo_mismo()
    {
        // Tres motivos distintos, un solo mensaje. Si difirieran, quien prueba ligas
        // sabria cuales existieron alguna vez y cuales no.
        var (caso, repo) = ArmarConsumo();

        var invitacion = await caso.RestablecerAsync(
            "bajio", repo.Emitir(PropositoToken.Invitacion), ContrasenaBuena, default);
        var caducado = await caso.RestablecerAsync(
            "bajio", repo.Emitir(minutosParaCaducar: -1), ContrasenaBuena, default);
        var inexistente = await caso.RestablecerAsync(
            "bajio", "token-que-nunca-existio", ContrasenaBuena, default);

        Assert.Equal(invitacion.Motivo, caducado.Motivo);
        Assert.Equal(caducado.Motivo, inexistente.Motivo);
    }

    [Fact]
    public async Task Restablecer_revoca_las_sesiones_de_refresco()
    {
        // Si alguien restablece porque le tomaron la cuenta y las sesiones del atacante
        // siguen vivas, el restablecimiento no sirvio de nada: el atacante conserva
        // acceso rotando su refresh token.
        var (caso, repo) = ArmarConsumo();

        var resultado = await caso.RestablecerAsync("bajio", repo.Emitir(), ContrasenaBuena, default);

        Assert.True(resultado.Correcto);
        Assert.Equal(repo.Usuarios[0].Id, Assert.Single(repo.SesionesRevocadasDe));
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("")]
    [InlineData("12345678901")]
    public async Task Una_contrasena_que_no_cumple_la_politica_no_quema_la_liga(string contrasena)
    {
        // La politica se comprueba ANTES de tocar el token. Si se quemara igual, un error
        // de captura obligaria a pedir otra liga y esperar otro correo.
        var (caso, repo) = ArmarConsumo();
        var token = repo.Emitir();

        var rechazo = await caso.RestablecerAsync("bajio", token, contrasena, default);
        var reintento = await caso.RestablecerAsync("bajio", token, ContrasenaBuena, default);

        Assert.False(rechazo.Correcto);
        Assert.True(reintento.Correcto);
    }

    [Theory]
    [InlineData(EstadoUsuario.Invitado)]
    [InlineData(EstadoUsuario.Suspendido)]
    [InlineData(EstadoUsuario.Baja)]
    public async Task Una_liga_vigente_de_alguien_que_ya_no_esta_activo_no_sirve(EstadoUsuario estado)
    {
        // El estado se comprueba al CONSUMIR y no solo al emitir: entre pedir la liga y
        // abrirla pasa hasta una hora, y suspender a alguien tiene que surtir efecto sin
        // esperar a que caduquen las ligas que ya tenia.
        var (caso, repo) = ArmarConsumo();
        var token = repo.Emitir();
        repo.Usuarios[0].Estado = estado;

        var resultado = await caso.RestablecerAsync("bajio", token, ContrasenaBuena, default);

        Assert.False(resultado.Correcto);
        Assert.Null(repo.Usuarios[0].HashContrasena);
    }

    // ------------------------------------------------------------ solicitar --

    [Fact]
    public async Task La_solicitud_se_comporta_igual_exista_o_no_el_correo()
    {
        // LA REGLA QUE SOSTIENE TODO EL FLUJO. Si la respuesta o el tiempo cambiaran, el
        // formulario de "olvide mi contrasena" se convierte en un enumerador de la lista
        // de empleados de un cliente —y probando slugs, de la lista de clientes—, que es
        // justo lo que evitan las tres reglas anti-filtracion del login.
        var (conCuenta, repoCon, buzonCon) = ArmarSolicitud();
        var (sinCuenta, repoSin, buzonSin) = ArmarSolicitud();

        var tardaCon = await Cronometrar(
            () => conCuenta.EjecutarAsync("bajio", new PeticionRestablecimiento(CorreoConCuenta), default));
        var tardaSin = await Cronometrar(
            () => sinCuenta.EjecutarAsync("bajio", new PeticionRestablecimiento("nadie@bajio.mx"), default));

        // El caso de uso no devuelve nada, asi que el endpoint no tiene sobre que
        // ramificar: la unica diferencia posible seria el tiempo.
        Assert.True(
            tardaCon >= SolicitarRestablecimiento.PisoDeRespuesta - Tolerancia,
            $"El camino con cuenta respondio en {tardaCon}, por debajo del piso.");
        Assert.True(
            tardaSin >= SolicitarRestablecimiento.PisoDeRespuesta - Tolerancia,
            $"El camino sin cuenta respondio en {tardaSin}, por debajo del piso.");
        Assert.True(
            (tardaCon - tardaSin).Duration() < Tolerancia,
            $"Diferencia observable entre los dos caminos: {tardaCon} contra {tardaSin}.");

        // Y la prueba no es vacua: en un caso hubo trabajo real y en el otro no.
        Assert.Single(buzonCon.Enviados);
        Assert.Empty(buzonSin.Enviados);
        Assert.Single(repoCon.Tokens);
        Assert.Empty(repoSin.Tokens);
    }

    [Fact]
    public async Task La_solicitud_a_una_empresa_inexistente_tampoco_se_distingue()
    {
        // Sin tenant resuelto no hay base que consultar, asi que esta rama es la que mas
        // barata saldria si no se igualara a proposito.
        var (caso, _, buzon) = ArmarSolicitud(empresaExiste: false);

        var tarda = await Cronometrar(
            () => caso.EjecutarAsync("no-existe", new PeticionRestablecimiento(CorreoConCuenta), default));

        Assert.True(tarda >= SolicitarRestablecimiento.PisoDeRespuesta - Tolerancia);
        Assert.Empty(buzon.Enviados);
    }

    [Fact]
    public async Task La_solicitud_emite_una_liga_de_una_hora()
    {
        // Dias, como la invitacion, alargaria la ventana en la que un correo interceptado
        // abre una cuenta que ya existe y que ya tiene datos dentro.
        var (caso, repo, _) = ArmarSolicitud();

        await caso.EjecutarAsync("bajio", new PeticionRestablecimiento(CorreoConCuenta), default);

        var vigencia = repo.Tokens[0].ExpiraEn - DateTime.UtcNow;

        Assert.InRange(vigencia, TimeSpan.FromMinutes(55), TimeSpan.FromMinutes(60));
        Assert.Equal(PropositoToken.RestablecerContrasena, repo.Tokens[0].Proposito);
    }

    [Fact]
    public async Task Pedir_otra_liga_invalida_la_anterior()
    {
        // Si no, quedan dos ligas validas circulando y la vieja —la que pudo ver quien
        // intercepto el primer correo— sigue abriendo la cuenta.
        var (caso, repo, _) = ArmarSolicitud();

        await caso.EjecutarAsync("bajio", new PeticionRestablecimiento(CorreoConCuenta), default);
        await caso.EjecutarAsync("bajio", new PeticionRestablecimiento(CorreoConCuenta), default);

        Assert.Equal(2, repo.Tokens.Count);
        Assert.NotNull(repo.Tokens[0].InvalidadoEn);
        Assert.Null(repo.Tokens[1].InvalidadoEn);
    }

    [Theory]
    [InlineData(EstadoUsuario.Invitado)]
    [InlineData(EstadoUsuario.Suspendido)]
    [InlineData(EstadoUsuario.Baja)]
    public async Task A_quien_no_esta_activo_no_se_le_manda_nada(EstadoUsuario estado)
    {
        // Un Invitado ya tiene su liga de invitacion; darle esta otra seria un segundo
        // camino para lo mismo. Suspendido y Baja no deben volver por ninguna puerta.
        var (caso, repo, buzon) = ArmarSolicitud();
        repo.Usuarios[0].Estado = estado;

        await caso.EjecutarAsync("bajio", new PeticionRestablecimiento(CorreoConCuenta), default);

        Assert.Empty(buzon.Enviados);
        Assert.Empty(repo.Tokens);
    }

    [Fact]
    public async Task Un_fallo_de_la_base_no_se_nota_desde_fuera()
    {
        // Una excepcion que subiera se volveria un 500, y un 500 que solo aparece cuando
        // la cuenta existe delata igual que un mensaje distinto.
        var (caso, repo, _) = ArmarSolicitud();
        repo.Revienta = true;

        var tarda = await Cronometrar(
            () => caso.EjecutarAsync("bajio", new PeticionRestablecimiento(CorreoConCuenta), default));

        Assert.True(tarda >= SolicitarRestablecimiento.PisoDeRespuesta - Tolerancia);
    }

    // ------------------------------------------------------------- andamio --

    /// <summary>
    /// Margen para el ruido del planificador y la granularidad de los temporizadores.
    /// Holgado a proposito: una prueba de tiempos que parpadea acaba borrada, y esta es
    /// de las que no conviene perder.
    /// </summary>
    private static readonly TimeSpan Tolerancia = TimeSpan.FromMilliseconds(250);

    private static async Task<TimeSpan> Cronometrar(Func<Task> accion)
    {
        var reloj = Stopwatch.StartNew();
        await accion();
        reloj.Stop();

        return reloj.Elapsed;
    }

    private static (Restablecimientos Caso, UsuariosFalsos Repo) ArmarConsumo()
    {
        var repo = new UsuariosFalsos();
        repo.AgregarUsuario(CorreoConCuenta);

        var caso = new Restablecimientos(
            new TenantFalso(true),
            () => repo,
            repo.Generador,
            new HashFalso(),
            NullLogger<Restablecimientos>.Instance);

        return (caso, repo);
    }

    private static (SolicitarRestablecimiento Caso, UsuariosFalsos Repo, BuzonFalso Buzon) ArmarSolicitud(
        bool empresaExiste = true)
    {
        var repo = new UsuariosFalsos();
        repo.AgregarUsuario(CorreoConCuenta);

        var buzon = new BuzonFalso();

        var caso = new SolicitarRestablecimiento(
            new TenantFalso(empresaExiste),
            () => repo,
            repo.Generador,
            new HashFalso(),
            new PlantillasFalsas(),
            buzon,
            NullLogger<SolicitarRestablecimiento>.Instance);

        return (caso, repo, buzon);
    }

    /// <summary>
    /// Reimplementa lo que hace UsuariosEmpresaEf, con una diferencia que importa: la
    /// condicion de vigencia NO se copia, se toma de TokenAcceso.Vigente, que es la misma
    /// que EF Core traduce a SQL. Asi la prueba no puede pasar sobre una regla que la
    /// produccion ya no aplica.
    /// </summary>
    private sealed class UsuariosFalsos : IUsuariosEmpresa
    {
        public readonly List<Usuario> Usuarios = [];

        public readonly List<TokenAcceso> Tokens = [];

        public readonly List<Guid> SesionesRevocadasDe = [];

        /// <summary>Para la prueba de que un fallo de base no se distingue.</summary>
        public bool Revienta { get; set; }

        /// <summary>El generador real: los tokens y sus hashes son de verdad.</summary>
        public readonly IGeneradorTokens Generador = new GeneradorTokensAleatorios();

        public void AgregarUsuario(string correo)
            => Usuarios.Add(new Usuario
            {
                Correo = correo,
                Nombre = "Ana",
                Estado = EstadoUsuario.Activo,
            });

        /// <summary>Emite un token directamente y devuelve el valor EN CLARO.</summary>
        public string Emitir(
            PropositoToken proposito = PropositoToken.RestablecerContrasena,
            int minutosParaCaducar = 60)
        {
            var generado = Generador.Generar();

            Tokens.Add(new TokenAcceso
            {
                UsuarioId = Usuarios[0].Id,
                Proposito = proposito,
                HashToken = generado.Hash,
                ExpiraEn = DateTime.UtcNow.AddMinutes(minutosParaCaducar),
            });

            return generado.EnClaro;
        }

        public Task<TokenConUsuario?> BuscarTokenVigenteAsync(
            string hashToken, PropositoToken proposito, CancellationToken ct)
        {
            var vigente = TokenAcceso.Vigente(proposito, DateTime.UtcNow).Compile();

            var token = Tokens.FirstOrDefault(t => t.HashToken == hashToken && vigente(t));

            return Task.FromResult(token is null
                ? null
                : (TokenConUsuario?)new TokenConUsuario(
                    token, Usuarios.First(u => u.Id == token.UsuarioId)));
        }

        public Task EmitirTokenAsync(
            Guid usuarioId, PropositoToken proposito, string hashToken, DateTime expiraEn,
            CancellationToken ct)
        {
            if (Revienta)
            {
                throw new InvalidOperationException("La base no responde.");
            }

            foreach (var pendiente in Tokens.Where(t =>
                t.UsuarioId == usuarioId && t.Proposito == proposito
                && t.UsadoEn is null && t.InvalidadoEn is null))
            {
                pendiente.InvalidadoEn = DateTime.UtcNow;
            }

            Tokens.Add(new TokenAcceso
            {
                UsuarioId = usuarioId,
                Proposito = proposito,
                HashToken = hashToken,
                ExpiraEn = expiraEn,
            });

            return Task.CompletedTask;
        }

        public Task RestablecerContrasenaAsync(
            Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct)
        {
            Usuarios.First(u => u.Id == usuarioId).HashContrasena = hashContrasena;
            Tokens.First(t => t.Id == tokenId).UsadoEn = DateTime.UtcNow;
            SesionesRevocadasDe.Add(usuarioId);

            return Task.CompletedTask;
        }

        public Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken ct)
            => Task.FromResult(Usuarios.FirstOrDefault(u => u.Correo == correo));

        public Task RevocarSesionesDeAsync(Guid usuarioId, CancellationToken ct)
        {
            SesionesRevocadasDe.Add(usuarioId);

            return Task.CompletedTask;
        }

        // ---- lo que este flujo no usa ----
        public Task AceptarInvitacionAsync(
            Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> PermisosDeAsync(Guid usuarioId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<bool> TieneAccesoTotalAsync(Guid usuarioId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task RegistrarAccesoAsync(
            Guid usuarioId, DateTime cuandoUtc, string? hashNuevo, CancellationToken ct)
            => throw new NotSupportedException();

        public Task CrearSesionAsync(SesionRefresh sesion, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<SesionRefresh?> BuscarSesionPorHashAsync(
            string hashToken, CancellationToken ct)
            => throw new NotSupportedException();

        public Task RotarSesionAsync(Guid anteriorId, SesionRefresh nueva, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class TenantFalso(bool resuelto) : IContextoTenant
    {
        public bool EstaResuelto => resuelto;

        public TenantResuelto Actual => resuelto
            ? new TenantResuelto(
                Guid.CreateVersion7(), "bajio", "maquinaria_bajio",
                "Maquinaria del Bajio SA de CV",
                EstadoTenant.Activo, EstadoAprovisionamiento.Lista,
                "America/Mexico_City", "MXN", new HashSet<string>(), new Dictionary<string, int>())
            : throw new InvalidOperationException("No hay tenant resuelto.");

        public void Establecer(TenantResuelto tenant) => throw new NotSupportedException();
    }

    /// <summary>
    /// Sin PBKDF2: 600 mil iteraciones por llamada harian que estas pruebas midieran el
    /// hash en lugar del piso de respuesta.
    /// </summary>
    private sealed class HashFalso : IHashContrasenas
    {
        public string Hash(string contrasena) => $"hash:{contrasena}";

        public ResultadoVerificacion Verificar(string hashAlmacenado, string contrasena)
            => new(hashAlmacenado == $"hash:{contrasena}", false);

        public void VerificarSenuelo(string contrasena)
        {
        }
    }

    private sealed class BuzonFalso : IEnviadorCorreo
    {
        public readonly List<MensajeCorreo> Enviados = [];

        public Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
        {
            Enviados.Add(mensaje);

            return Task.FromResult(ResultadoEnvio.Ok("prueba"));
        }
    }

    private sealed class PlantillasFalsas : IPlantillasCorreo
    {
        public bool DevuelveLigaEnRespuesta => false;

        public string LigaDeInvitacion(string slug, string tokenEnClaro)
            => $"https://{slug}.ejemplo.mx/invitacion?token={tokenEnClaro}";

        public MensajeCorreo Invitacion(string para, string razonSocial, string liga)
            => new(para, "invitacion", liga, liga);

        public string LigaDeRestablecimiento(string slug, string tokenEnClaro)
            => $"https://{slug}.ejemplo.mx/restablecer?token={tokenEnClaro}";

        public MensajeCorreo Restablecimiento(string para, string razonSocial, string liga)
            => new(para, "restablecimiento", liga, liga);
    }
}
