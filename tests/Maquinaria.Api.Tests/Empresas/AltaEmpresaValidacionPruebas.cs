using Maquinaria.Aplicacion.Correo;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Comun;
using Maquinaria.Dominio.Plataforma;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El CABLEADO de los tres formatos en el alta de empresas, que es lo que las pruebas de
/// FormatoRfc, FormatoTelefono y FormatoCorreo no pueden ver por si solas.
///
/// Lo que se fija es que el rechazo pase ANTES de tocar la base: los dobles de aqui
/// revientan con NotSupportedException en cuanto alguien los llama, asi que si una
/// validacion se cayera del sitio, la prueba no fallaria con un Assert amable sino con la
/// excepcion del doble. Esa es la gracia.
///
/// Un rechazo por formato NO es un fallo del sistema: se comprueba EsRechazo, que es lo que
/// el endpoint traduce a 400 con el titulo "Alta rechazada" en lugar de a un 500.
/// </summary>
public class AltaEmpresaValidacionPruebas
{
    [Theory]
    [InlineData("MDB12031AB1")]                  // 11 caracteres
    [InlineData("LOPZM850612H45")]               // 14
    [InlineData("MDBAB0315AB1")]                 // letras en la zona de la fecha
    [InlineData("cualquier cosa")]
    public async Task Un_rfc_invalido_se_rechaza_antes_del_insert(string rfc)
    {
        var resultado = await Caso().EjecutarAsync(Alta(rfc: rfc), default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(FormatoRfc.Explicacion, resultado.Motivo);
    }

    [Theory]
    [InlineData("llamar al Beto")]               // la queja que llego de la interfaz
    [InlineData("123456789")]                    // 9 digitos
    [InlineData("1234567890123456")]             // 16
    public async Task Un_telefono_invalido_se_rechaza_antes_del_insert(string telefono)
    {
        var resultado = await Caso().EjecutarAsync(Alta(telefono: telefono), default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(FormatoTelefono.Explicacion, resultado.Motivo);
    }

    [Theory]
    [InlineData("hola")]                         // la que pasaba con solo IsNullOrWhiteSpace
    [InlineData("a@b")]
    [InlineData("a@b.c")]
    [InlineData("ana lopez@bajio.mx")]
    public async Task Un_correo_de_administrador_invalido_se_rechaza(string correo)
    {
        var resultado = await Caso().EjecutarAsync(Alta(correo: correo), default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(FormatoCorreo.Explicacion, resultado.Motivo);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public async Task El_rfc_y_el_telefono_son_opcionales(string? rfc, string? telefono)
    {
        // Vacio o nulo NO es un rechazo: pasa de largo y llega hasta ExisteSlugAsync, que es
        // lo primero que toca la base y lo que el doble usa para reventar. Que la excepcion
        // sea esa y no un rechazo es la senal de que la validacion lo dejo pasar.
        await Assert.ThrowsAsync<NotSupportedException>(
            () => Caso().EjecutarAsync(Alta(rfc: rfc, telefono: telefono), default));
    }

    [Fact]
    public async Task Un_rfc_y_un_telefono_validos_dejan_pasar_el_alta()
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => Caso().EjecutarAsync(
                Alta(rfc: " mdb 120315 ab1 ", telefono: " 4771234567 "), default));
    }

    [Fact]
    public async Task El_reintento_valida_el_correo_igual_que_el_alta()
    {
        // Es el mismo dato con la misma consecuencia: ese buzon recibe la liga de
        // invitacion. Validarlo en el alta y no aqui dejaria la puerta de al lado abierta.
        var resultado = await Caso().ReintentarAsync(
            "bajio", new ReintentoDeAlta("hola", "Ana Admin"), default);

        Assert.True(resultado.EsRechazo);
        Assert.Equal(FormatoCorreo.Explicacion, resultado.Motivo);
    }

    // ------------------------------------------------------------- andamio --

    private static AltaDeEmpresa Alta(
        string? rfc = null, string? telefono = null, string correo = "admin@bajio.mx")
        => new(
            Slug: "bajio",
            RazonSocial: "Maquinaria del Bajio SA de CV",
            NombreComercial: null,
            Rfc: rfc,
            Telefono: telefono,
            CorreoContacto: null,
            CorreoAdministrador: correo,
            NombreAdministrador: "Ana Admin",
            CodigoPlan: "base");

    private static AprovisionarEmpresa Caso()
        => new(
            new RegistroQueRevienta(),
            new BasesQueRevientan(),
            new SembradorQueRevienta(),
            new BuzonQueRevienta(),
            new PlantillasQueRevientan(),
            new DirectorioQueRevienta(),
            NullLogger<AprovisionarEmpresa>.Instance);

    /// <summary>
    /// TODO revienta. Si una de las validaciones dejara de correr antes del INSERT, la
    /// prueba no diria "esperaba un rechazo": diria NotSupportedException, que es la senal
    /// de que el dato malo llego a la base.
    /// </summary>
    private sealed class RegistroQueRevienta : IRegistroTenants
    {
        public Task MarcarInvitacionEnviadaAsync(
            Guid tenantId, bool enviada, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<bool> ExisteSlugAsync(string slug, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Plan?> BuscarPlanPorCodigoAsync(string codigo, CancellationToken ct)
            => throw new NotSupportedException();

        public Task CrearAsync(Tenant nuevo, Suscripcion suscripcion, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<Tenant?> BuscarPorSlugAsync(string slug, CancellationToken ct)
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

    private sealed class BasesQueRevientan : IAprovisionadorBaseDatos
    {
        public Task<bool> ExisteBaseAsync(string nombreBd, CancellationToken ct)
            => throw new NotSupportedException();

        public Task CrearBaseAsync(string nombreBd, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<string> MigrarAsync(string nombreBd, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<string?> VersionAplicadaAsync(string nombreBd, CancellationToken ct)
            => throw new NotSupportedException();

        public IReadOnlyList<string> VersionesDisponibles() => throw new NotSupportedException();
    }

    private sealed class SembradorQueRevienta : ISembradorAdministrador
    {
        public Task<AdministradorSembrado> CrearAdministradorAsync(
            string nombreBd, string correo, string nombre, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ResultadoReemision> ReemitirInvitacionAsync(
            string nombreBd, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class BuzonQueRevienta : IEnviadorCorreo
    {
        public Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class PlantillasQueRevientan : IPlantillasCorreo
    {
        public bool DevuelveLigaEnRespuesta => false;

        public string LigaDeInvitacion(string slug, string tokenEnClaro)
            => throw new NotSupportedException();

        public MensajeCorreo Invitacion(string para, string razonSocial, string liga)
            => throw new NotSupportedException();

        public string LigaDeRestablecimiento(string slug, string tokenEnClaro)
            => throw new NotSupportedException();

        public MensajeCorreo Restablecimiento(string para, string razonSocial, string liga)
            => throw new NotSupportedException();
    }

    private sealed class DirectorioQueRevienta : IDirectorioTenants
    {
        public Task<TenantResuelto?> BuscarPorSlugAsync(string slug, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<TenantResuelto?> BuscarPorIdAsync(Guid id, CancellationToken ct)
            => throw new NotSupportedException();

        public void Invalidar(Guid id, string slug) => throw new NotSupportedException();
    }
}
