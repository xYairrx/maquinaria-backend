using Maquinaria.Aplicacion.Correo;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Plataforma;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El reenvio de la invitacion del administrador de una empresa.
///
/// LO QUE MAS IMPORTA FIJAR AQUI es que el destinatario sale de la BASE y no de la peticion.
/// Este flujo nace de un agujero que ya se cerro una vez en el reintento del alta: con un
/// parametro de correo, quien tuviera acceso al panel pedia la liga de una cuenta con acceso
/// total a su propio buzon y le definia la contrasena. El metodo no recibe correo, y estas
/// pruebas son lo que impide que alguien se lo agregue «por comodidad».
///
/// Lo segundo: solo a quien sigue Invitado. Una invitacion a una cuenta ACTIVA seria un
/// segundo camino para definir contrasena sin conocer la actual.
/// </summary>
public class ReenvioInvitacionPruebas
{
    private const string Slug = "bajio";
    private const string NombreBd = "maquinaria_bajio";
    private const string CorreoEnLaBase = "admin.real@bajio.mx";

    private static Tenant TenantListo(
        EstadoAprovisionamiento estado = EstadoAprovisionamiento.Lista) => new()
        {
            Id = Guid.NewGuid(),
            Slug = Slug,
            NombreBd = NombreBd,
            RazonSocial = "Maquinaria del Bajio SA de CV",
            Estado = EstadoTenant.Activo,
            EstadoAprovisionamiento = estado,
        };

    private static ReenviarInvitacion Construir(
        Tenant? tenant, SembradorFalso sembrador, BuzonFalso buzon) =>
        Construir(tenant, sembrador, buzon, new RegistroFalso(tenant));

    private static ReenviarInvitacion Construir(
        Tenant? tenant, SembradorFalso sembrador, BuzonFalso buzon, RegistroFalso registro) =>
        new(registro, sembrador, new PlantillasFalsas(), buzon,
            NullLogger<ReenviarInvitacion>.Instance);

    [Fact]
    public async Task Manda_al_correo_de_la_base_y_no_a_ninguno_de_entrada()
    {
        var sembrador = new SembradorFalso(ResultadoReemision.Exito(CorreoEnLaBase, "tok"));
        var buzon = new BuzonFalso();

        var r = await Construir(TenantListo(), sembrador, buzon).EjecutarAsync(Slug, default);

        Assert.True(r.Correcto);
        Assert.True(r.InvitacionEnviada);

        // El correo del resultado y el del envio son el de la base. No hay ningun otro sitio
        // de donde pudieran haber salido: el metodo no acepta un correo.
        Assert.Equal(CorreoEnLaBase, r.Correo);
        Assert.Equal(CorreoEnLaBase, buzon.UltimoDestinatario);
        Assert.Equal(NombreBd, sembrador.UltimaBase);
    }

    [Fact]
    public async Task Un_slug_mal_formado_se_rechaza_sin_tocar_la_base()
    {
        var sembrador = new SembradorFalso(ResultadoReemision.Exito(CorreoEnLaBase, "tok"));

        var r = await Construir(TenantListo(), sembrador, new BuzonFalso())
            .EjecutarAsync("NO VALIDO", default);

        Assert.False(r.Correcto);
        Assert.Null(sembrador.UltimaBase);
    }

    [Fact]
    public async Task Una_empresa_que_no_existe_se_rechaza()
    {
        var sembrador = new SembradorFalso(ResultadoReemision.Exito(CorreoEnLaBase, "tok"));

        var r = await Construir(null, sembrador, new BuzonFalso()).EjecutarAsync(Slug, default);

        Assert.False(r.Correcto);
        Assert.Null(sembrador.UltimaBase);
    }

    [Theory]
    [InlineData(EstadoAprovisionamiento.Pendiente)]
    [InlineData(EstadoAprovisionamiento.Creando)]
    [InlineData(EstadoAprovisionamiento.Fallida)]
    public async Task Sin_base_lista_no_se_intenta_abrirla(EstadoAprovisionamiento estado)
    {
        var sembrador = new SembradorFalso(ResultadoReemision.Exito(CorreoEnLaBase, "tok"));

        var r = await Construir(TenantListo(estado), sembrador, new BuzonFalso())
            .EjecutarAsync(Slug, default);

        // Lo que le falta a esa empresa es terminar su alta, y abrir su base daria un error
        // de conexion que no dice eso.
        Assert.False(r.Correcto);
        Assert.Null(sembrador.UltimaBase);
    }

    [Fact]
    public async Task El_motivo_del_sembrador_se_propaga_tal_cual()
    {
        // Es el camino del administrador ya ACTIVO: el mensaje tiene que llegar a la
        // interfaz sin reescribirse, porque le dice a quien mira que use el
        // restablecimiento de contrasena en lugar de esta puerta.
        var sembrador = new SembradorFalso(
            ResultadoReemision.Rechazado("El administrador ya activo su cuenta."));
        var buzon = new BuzonFalso();

        var r = await Construir(TenantListo(), sembrador, buzon).EjecutarAsync(Slug, default);

        Assert.False(r.Correcto);
        Assert.Equal("El administrador ya activo su cuenta.", r.Motivo);
        Assert.Null(buzon.UltimoDestinatario);
    }

    [Fact]
    public async Task Si_el_correo_no_sale_el_reenvio_es_correcto_pero_lo_dice()
    {
        var sembrador = new SembradorFalso(ResultadoReemision.Exito(CorreoEnLaBase, "tok"));
        var buzon = new BuzonFalso { Falla = true };

        var r = await Construir(TenantListo(), sembrador, buzon).EjecutarAsync(Slug, default);

        // Correcto pero NO enviada, y la distincion importa: la invitacion anterior ya quedo
        // invalidada, asi que quien mira tiene que saber que hay que reenviar otra vez y no
        // creer que la liga vieja sigue sirviendo.
        Assert.True(r.Correcto);
        Assert.False(r.InvitacionEnviada);
    }

    [Fact]
    public async Task Deja_marcado_que_la_invitacion_SI_salio()
    {
        var tenant = TenantListo();
        var registro = new RegistroFalso(tenant);
        var sembrador = new SembradorFalso(ResultadoReemision.Exito(CorreoEnLaBase, "tok"));

        await Construir(tenant, sembrador, new BuzonFalso(), registro)
            .EjecutarAsync(Slug, default);

        Assert.True(registro.Marcado);
    }

    [Fact]
    public async Task Deja_marcado_el_FALLO_y_no_solo_los_exitos()
    {
        var tenant = TenantListo();
        var registro = new RegistroFalso(tenant);
        var sembrador = new SembradorFalso(ResultadoReemision.Exito(CorreoEnLaBase, "tok"));

        await Construir(tenant, sembrador, new BuzonFalso { Falla = true }, registro)
            .EjecutarAsync(Slug, default);

        // Guardar solo los exitos dejaria el fallo indistinguible de «todavia no se intento»,
        // que es el estado del que esta columna vino a sacarnos: el panel volveria a no poder
        // ofrecer el reenvio justo donde hace falta.
        Assert.False(registro.Marcado);
    }

    // ------------------------------------------------------------------
    // Falsos
    // ------------------------------------------------------------------

    private sealed class SembradorFalso(ResultadoReemision resultado) : ISembradorAdministrador
    {
        public string? UltimaBase { get; private set; }

        public Task<ResultadoReemision> ReemitirInvitacionAsync(
            string nombreBd, CancellationToken ct)
        {
            UltimaBase = nombreBd;

            return Task.FromResult(resultado);
        }

        public Task<AdministradorSembrado> CrearAdministradorAsync(
            string nombreBd, string correo, string nombre, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class BuzonFalso : IEnviadorCorreo
    {
        public bool Falla { get; set; }

        public string? UltimoDestinatario { get; private set; }

        public Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
        {
            UltimoDestinatario = mensaje.Para;

            return Task.FromResult(
                Falla ? ResultadoEnvio.Fallo("prueba") : ResultadoEnvio.Ok("prueba"));
        }
    }

    private sealed class PlantillasFalsas : IPlantillasCorreo
    {
        public bool DevuelveLigaEnRespuesta => false;

        public string LigaDeInvitacion(string slug, string tokenEnClaro)
            => $"https://{slug}.ejemplo/invitacion?token={tokenEnClaro}";

        public MensajeCorreo Invitacion(string para, string razonSocial, string liga)
            => new(para, "Tu acceso", liga, liga);

        public string LigaDeRestablecimiento(string slug, string tokenEnClaro)
            => throw new NotSupportedException();

        public MensajeCorreo Restablecimiento(string para, string razonSocial, string liga)
            => throw new NotSupportedException();
    }

    private sealed class RegistroFalso(Tenant? tenant) : IRegistroTenants
    {
        /// <summary>Lo ultimo que se marco, o null si no se marco nada.</summary>
        public bool? Marcado { get; private set; }

        public Task<Tenant?> BuscarPorSlugAsync(string slug, CancellationToken ct)
            => Task.FromResult(tenant?.Slug == slug ? tenant : null);

        public Task MarcarInvitacionEnviadaAsync(Guid tenantId, bool enviada, CancellationToken ct)
        {
            Marcado = enviada;

            return Task.CompletedTask;
        }

        // ---- nada de esto lo usa el reenvio ----
        public Task<bool> ExisteSlugAsync(string slug, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Plan?> BuscarPlanPorCodigoAsync(string codigo, CancellationToken ct)
            => throw new NotSupportedException();

        public Task CrearAsync(Tenant nuevo, Suscripcion suscripcion, CancellationToken ct)
            => throw new NotSupportedException();

        public Task CambiarEstadoAprovisionamientoAsync(
            Guid tenantId, EstadoAprovisionamiento estado, CancellationToken ct)
            => throw new NotSupportedException();

        public Task MarcarListaAsync(Guid tenantId, string versionEsquema, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ResumenEmpresa>> ListarAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<TenantParaMigrar>> ListarParaMigrarAsync(
            string? slug, CancellationToken ct)
            => throw new NotSupportedException();

        public Task MarcarVersionEsquemaAsync(Guid tenantId, string version, CancellationToken ct)
            => throw new NotSupportedException();

        public bool EsColisionDeUnicidad(Exception e) => false;
    }
}
