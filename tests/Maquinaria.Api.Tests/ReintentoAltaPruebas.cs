using Maquinaria.Aplicacion.Correo;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Plataforma;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El reintento de un alta que quedo en Fallida.
///
/// Se prueba sin base de datos: lo que hay que fijar son las GUARDAS —desde que estado se
/// reintenta y desde cuales no— y que la secuencia que corre es la misma del alta, no una
/// copia. El aprovisionamiento de verdad se verifica contra Neon, como dice la bitacora.
/// </summary>
public class ReintentoAltaPruebas
{
    private static readonly ReintentoDeAlta Datos = new("admin@bajio.mx", "Ana Admin");

    [Fact]
    public async Task Un_alta_en_Fallida_se_reintenta()
    {
        var (caso, registro, bases, sembrador) = Armar(EstadoAprovisionamiento.Fallida);

        var resultado = await caso.ReintentarAsync("bajio", Datos, default);

        Assert.True(resultado.Correcto);

        // La secuencia completa, y en el orden que importa: pasa por Creando antes de
        // tocar la base y termina en Lista con su version de esquema.
        Assert.Equal(EstadoAprovisionamiento.Lista, registro.Estado);
        Assert.Equal("EmpresaCatalogosOrganizacion", registro.VersionEsquema);
        Assert.Equal([EstadoAprovisionamiento.Creando], registro.Transiciones);

        // Y NO vuelve a crear la base: la reutiliza, que es de lo que depende que el
        // reintento sea idempotente.
        Assert.Equal(0, bases.Creadas);
        Assert.Equal(1, bases.Migradas);
        Assert.Equal("maquinaria_bajio", sembrador.UltimaBase);
    }

    [Theory]
    [InlineData(EstadoAprovisionamiento.Lista)]
    [InlineData(EstadoAprovisionamiento.Creando)]
    [InlineData(EstadoAprovisionamiento.Pendiente)]
    public async Task Un_alta_que_no_esta_en_Fallida_no_se_reintenta(EstadoAprovisionamiento estado)
    {
        // Lista es el caso peligroso: reintentar sobre una empresa que ya opera reemitiria
        // la invitacion de su administrador, y quien tenga acceso al panel podria tomar esa
        // cuenta sin conocer su contrasena. Creando se solaparia con el intento que corre.
        var (caso, registro, bases, sembrador) = Armar(estado);

        var resultado = await caso.ReintentarAsync("bajio", Datos, default);

        Assert.False(resultado.Correcto);
        Assert.True(resultado.EsRechazo);
        Assert.Contains(estado.ToString(), resultado.Motivo);

        // Nada se toco: ni el estado, ni la base, ni el administrador.
        Assert.Equal(estado, registro.Estado);
        Assert.Empty(registro.Transiciones);
        Assert.Equal(0, bases.Migradas);
        Assert.Null(sembrador.UltimaBase);
    }

    [Fact]
    public async Task Una_empresa_que_no_existe_no_se_reintenta()
    {
        var (caso, _, bases, _) = Armar(EstadoAprovisionamiento.Fallida, existe: false);

        var resultado = await caso.ReintentarAsync("bajio", Datos, default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(0, bases.Migradas);
    }

    [Fact]
    public async Task Una_empresa_dada_de_baja_no_se_reintenta()
    {
        // Su base y su historial siguen existiendo, asi que reaprovisionarla no es
        // reintentar un alta: es reactivar un cliente, que es otra operacion.
        var (caso, registro, bases, _) = Armar(EstadoAprovisionamiento.Fallida);
        registro.Tenant!.EliminadoEn = DateTime.UtcNow;

        var resultado = await caso.ReintentarAsync("bajio", Datos, default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(0, bases.Migradas);
    }

    [Theory]
    [InlineData("BAJIO")]
    [InlineData("  bajio  ")]
    public async Task El_slug_se_normaliza_antes_de_buscar(string slug)
    {
        // El slug viene de la ruta. Sin normalizar, /BAJIO/reintento diria que la empresa
        // no existe cuando si existe.
        var (caso, _, bases, _) = Armar(EstadoAprovisionamiento.Fallida);

        var resultado = await caso.ReintentarAsync(slug, Datos, default);

        Assert.True(resultado.Correcto);
        Assert.Equal(1, bases.Migradas);
    }

    [Theory]
    [InlineData("bajio; DROP DATABASE postgres; --")]
    [InlineData("mala_empresa")]
    [InlineData("-bajio")]
    [InlineData("")]
    public async Task Un_slug_con_formato_invalido_se_rechaza_antes_de_tocar_nada(string slug)
    {
        // El nombre de la base se concatena en un CREATE DATABASE porque los
        // identificadores SQL no se parametrizan. Revalidar el formato en C# es control de
        // seguridad, no cosmetica, y el reintento es el unico camino que parte de un slug
        // que no acaba de pasar por la validacion del alta.
        var (caso, _, bases, _) = Armar(EstadoAprovisionamiento.Fallida);

        var resultado = await caso.ReintentarAsync(slug, Datos, default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(0, bases.Creadas);
        Assert.Equal(0, bases.Migradas);
    }

    [Fact]
    public async Task Un_nombre_de_base_que_no_deriva_del_slug_se_rechaza()
    {
        // Segunda linea de la misma defensa: aunque el slug sea valido, el nombre_bd que
        // trae la fila tiene que ser exactamente el que se derivaria de el. Si no coincide,
        // alguien escribio en la central por un camino que no es este codigo.
        var (caso, registro, bases, _) = Armar(EstadoAprovisionamiento.Fallida);
        registro.Tenant!.NombreBd = "maquinaria_otra_cosa";

        var resultado = await caso.ReintentarAsync("bajio", Datos, default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(0, bases.Creadas);
        Assert.Equal(0, bases.Migradas);
    }

    [Theory]
    [InlineData("", "Ana")]
    [InlineData("admin@bajio.mx", "")]
    [InlineData("   ", "   ")]
    public async Task Sin_administrador_no_se_reintenta(string correo, string nombre)
    {
        // Es lo unico que el reintento pide, porque la central no guarda a quien se invito.
        // Sin ese dato, la secuencia llegaria al paso 4 y reventaria a mitad de camino.
        var (caso, _, bases, _) = Armar(EstadoAprovisionamiento.Fallida);

        var resultado = await caso.ReintentarAsync(
            "bajio", new ReintentoDeAlta(correo, nombre), default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(0, bases.Migradas);
    }

    [Fact]
    public async Task Un_reintento_que_vuelve_a_fallar_deja_el_registro_en_Fallida()
    {
        // Reintentable otra vez. Si el fallo lo dejara en Creando, el panel lo mostraria
        // como un aprovisionamiento colgado y nadie podria volver a dispararlo.
        var (caso, registro, bases, _) = Armar(EstadoAprovisionamiento.Fallida);
        bases.Revienta = true;

        var resultado = await caso.ReintentarAsync("bajio", Datos, default);

        Assert.False(resultado.Correcto);
        Assert.False(resultado.EsRechazo);
        Assert.Equal(EstadoAprovisionamiento.Fallida, registro.Estado);
    }

    [Fact]
    public async Task El_reintento_no_toca_la_suscripcion_ni_el_plan()
    {
        // Los pasos que se repiten son del 2 al 6. El 1 —tenant y suscripcion— es lo unico
        // atomico de la secuencia y ya quedo hecho; volver a aceptar el plan aqui seria una
        // forma de cambiarle el contrato a una empresa por la puerta de atras del reintento.
        var (caso, registro, _, _) = Armar(EstadoAprovisionamiento.Fallida);

        await caso.ReintentarAsync("bajio", Datos, default);

        Assert.Equal(0, registro.PlanesConsultados);
        Assert.Equal(0, registro.Creados);
    }

    // ------------------------------------------------------------- andamio --

    private static (AprovisionarEmpresa Caso, RegistroFalso Registro, BasesFalsas Bases,
        SembradorFalso Sembrador) Armar(
        EstadoAprovisionamiento estado, bool existe = true)
    {
        var registro = new RegistroFalso(existe ? TenantEn(estado) : null);
        var bases = new BasesFalsas();
        var sembrador = new SembradorFalso();

        var caso = new AprovisionarEmpresa(
            registro,
            bases,
            sembrador,
            new BuzonFalso(),
            new PlantillasFalsas(),
            new DirectorioFalso(),
            NullLogger<AprovisionarEmpresa>.Instance);

        return (caso, registro, bases, sembrador);
    }

    private static Tenant TenantEn(EstadoAprovisionamiento estado)
        => new()
        {
            Slug = "bajio",
            NombreBd = "maquinaria_bajio",
            RazonSocial = "Maquinaria del Bajio SA de CV",
            Estado = EstadoTenant.Prueba,
            EstadoAprovisionamiento = estado,
        };

    private sealed class RegistroFalso(Tenant? tenant) : IRegistroTenants
    {
        public Tenant? Tenant => tenant;

        public EstadoAprovisionamiento Estado
            => tenant?.EstadoAprovisionamiento ?? EstadoAprovisionamiento.Pendiente;

        /// <summary>Por donde paso el estado, en orden, sin contar el final.</summary>
        public readonly List<EstadoAprovisionamiento> Transiciones = [];

        public string? VersionEsquema { get; private set; }

        public int PlanesConsultados { get; private set; }

        public int Creados { get; private set; }

        public Task<Tenant?> BuscarPorSlugAsync(string slug, CancellationToken ct)
            => Task.FromResult(tenant?.Slug == slug ? tenant : null);

        public Task CambiarEstadoAprovisionamientoAsync(
            Guid tenantId, EstadoAprovisionamiento estado, CancellationToken ct)
        {
            Transiciones.Add(estado);
            tenant!.EstadoAprovisionamiento = estado;

            return Task.CompletedTask;
        }

        public Task MarcarListaAsync(Guid tenantId, string versionEsquema, CancellationToken ct)
        {
            tenant!.EstadoAprovisionamiento = EstadoAprovisionamiento.Lista;
            VersionEsquema = versionEsquema;

            return Task.CompletedTask;
        }

        public Task<Plan?> BuscarPlanPorCodigoAsync(string codigo, CancellationToken ct)
        {
            PlanesConsultados++;

            return Task.FromResult<Plan?>(null);
        }

        public Task CrearAsync(Tenant nuevo, Suscripcion suscripcion, CancellationToken ct)
        {
            Creados++;

            return Task.CompletedTask;
        }

        public Task<bool> ExisteSlugAsync(string slug, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<ResumenEmpresa>> ListarAsync(CancellationToken ct)
            => throw new NotSupportedException();

        // ---- lo que usa migrar-empresas, no el reintento ----
        public Task<IReadOnlyList<TenantParaMigrar>> ListarParaMigrarAsync(
            string? slug, CancellationToken ct)
            => throw new NotSupportedException();

        public Task MarcarVersionEsquemaAsync(
            Guid tenantId, string version, CancellationToken ct)
            => throw new NotSupportedException();

        public bool EsColisionDeUnicidad(Exception e) => false;
    }

    private sealed class BasesFalsas : IAprovisionadorBaseDatos
    {
        public int Creadas { get; private set; }

        public int Migradas { get; private set; }

        /// <summary>Para el caso del reintento que vuelve a fallar.</summary>
        public bool Revienta { get; set; }

        /// <summary>
        /// Siempre true: el reintento tipico es de un alta que ya creo la base. Es lo que
        /// hace visible que la secuencia la REUSA en lugar de volver a crearla.
        /// </summary>
        public Task<bool> ExisteBaseAsync(string nombreBd, CancellationToken ct)
            => Task.FromResult(true);

        public Task CrearBaseAsync(string nombreBd, CancellationToken ct)
        {
            Creadas++;

            return Task.CompletedTask;
        }

        public Task<string> MigrarAsync(string nombreBd, CancellationToken ct)
        {
            if (Revienta)
            {
                throw new InvalidOperationException("No se pudo migrar.");
            }

            Migradas++;

            return Task.FromResult("EmpresaCatalogosOrganizacion");
        }

        // ---- lo que usa migrar-empresas, no el reintento ----
        public Task<string?> VersionAplicadaAsync(string nombreBd, CancellationToken ct)
            => throw new NotSupportedException();

        public IReadOnlyList<string> VersionesDisponibles() => throw new NotSupportedException();
    }

    private sealed class SembradorFalso : ISembradorAdministrador
    {
        public string? UltimaBase { get; private set; }

        public Task<AdministradorSembrado> CrearAdministradorAsync(
            string nombreBd, string correo, string nombre, CancellationToken ct)
        {
            UltimaBase = nombreBd;

            return Task.FromResult(new AdministradorSembrado(correo, "token-en-claro"));
        }
    }

    private sealed class BuzonFalso : IEnviadorCorreo
    {
        public Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
            => Task.FromResult(ResultadoEnvio.Ok("prueba"));
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

    private sealed class DirectorioFalso : IDirectorioTenants
    {
        public Task<TenantResuelto?> BuscarPorSlugAsync(string slug, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<TenantResuelto?> BuscarPorIdAsync(Guid id, CancellationToken ct)
            => throw new NotSupportedException();

        public void Invalidar(Guid id, string slug)
        {
        }
    }
}
