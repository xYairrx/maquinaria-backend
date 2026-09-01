using System.Net;
using System.Text.Json;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Seguridad;
using Maquinaria.Dominio.Trazabilidad;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Api.Tests;

/// <summary>
/// Que el interceptor escriba la fila correcta, y sobre todo QUE NO ESCRIBA LOS HASHES.
///
/// La exclusion es la unica parte de esta clase con consecuencia de seguridad: si se
/// rompe, los hashes de contrasena acaban en el jsonb de la tabla que nunca se borra,
/// que es exactamente lo que guardarlos hasheados pretende evitar. Un descuido ahi no
/// da error ni se ve en pantalla.
///
/// SIN BASE DE DATOS, con el mismo truco que las pruebas de traduccion: el ChangeTracker
/// no abre conexion, asi que basta un contexto apuntado a un puerto muerto.
/// </summary>
public class InterceptorAuditoriaPruebas
{
    private const string CadenaMuerta =
        "Host=127.0.0.1;Port=1;Database=nada;Username=nadie;Password=x;Timeout=1;";

    private const string HashSecreto = "$pbkdf2$no-debe-aparecer-en-la-bitacora";

    [Fact]
    public void Un_alta_registra_la_entidad_su_llave_y_el_actor()
    {
        using var contexto = Contexto();

        var usuario = NuevoUsuario();
        contexto.Usuarios.Add(usuario);

        var actor = new ActorFalso();
        new InterceptorAuditoria(actor).Registrar(contexto);

        var fila = Bitacora(contexto);

        Assert.Equal(AccionAuditoria.Alta, fila.Accion);
        Assert.Equal("Usuario", fila.Entidad);
        Assert.Equal(usuario.Id.ToString(), fila.EntidadId);
        Assert.Equal(OrigenesAuditoria.Api, fila.Origen);
        Assert.Equal(actor.CorrelacionId, fila.CorrelacionId);
        Assert.Equal(actor.UsuarioId, fila.UsuarioId);
        Assert.Equal(["administrador"], fila.Roles);

        // El patron de nulos dice la accion sin mirar Accion: en un alta no hay
        // subconjunto anterior que enviar.
        Assert.Null(fila.ValoresAnteriores);
        Assert.NotNull(fila.ValoresNuevos);
    }

    [Fact]
    public void El_hash_de_la_contrasena_NUNCA_entra_al_jsonb()
    {
        using var contexto = Contexto();

        contexto.Usuarios.Add(NuevoUsuario());

        new InterceptorAuditoria(new ActorFalso()).Registrar(contexto);

        var nuevos = Bitacora(contexto).ValoresNuevos!;

        Assert.DoesNotContain(HashSecreto, nuevos, StringComparison.Ordinal);
        Assert.DoesNotContain("HashContrasena", nuevos, StringComparison.Ordinal);

        // Y que la exclusion no se haya llevado por delante el resto de la fila.
        Assert.Contains("Correo", nuevos, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_cambio_registra_solo_lo_que_cambio()
    {
        using var contexto = Contexto();

        var usuario = NuevoUsuario();
        contexto.Attach(usuario);

        usuario.Nombre = "Nombre nuevo";

        new InterceptorAuditoria(new ActorFalso()).Registrar(contexto);

        var fila = Bitacora(contexto);

        Assert.Equal(AccionAuditoria.Cambio, fila.Accion);

        var anteriores = Claves(fila.ValoresAnteriores!);
        var nuevos = Claves(fila.ValoresNuevos!);

        Assert.Equal(["Nombre"], anteriores);
        Assert.Equal(["Nombre"], nuevos);
    }

    [Fact]
    public void Un_cambio_que_SOLO_toca_un_campo_excluido_no_deja_fila()
    {
        using var contexto = Contexto();

        var usuario = NuevoUsuario();
        contexto.Attach(usuario);

        // Es lo que hace un rehash al iniciar sesion. Sin la comprobacion de
        // "modificadas.Count == 0" quedaria una fila con los dos jsonb vacios, que en
        // el patron de nulos significa un evento de los que el interceptor no escribe.
        usuario.HashContrasena = "$pbkdf2$otro-costo";

        new InterceptorAuditoria(new ActorFalso()).Registrar(contexto);

        Assert.Empty(contexto.ChangeTracker.Entries<Auditoria>());
    }

    [Fact]
    public void Sin_actor_establecido_el_origen_es_sistema()
    {
        using var contexto = Contexto();

        contexto.Usuarios.Add(NuevoUsuario());

        // Es el caso de migrar-empresas y de cualquier proceso sin peticion detras: el
        // portador real nace asi, sin usuario y con roles vacios.
        new InterceptorAuditoria(
            new ActorFalso { EstaEstablecido = false, UsuarioId = null, Roles = [] })
            .Registrar(contexto);

        var fila = Bitacora(contexto);

        Assert.Equal(OrigenesAuditoria.Sistema, fila.Origen);
        Assert.Null(fila.UsuarioId);
        Assert.Empty(fila.Roles);
    }

    [Fact]
    public void Un_superadministrador_dentro_de_una_empresa_es_origen_plataforma()
    {
        using var contexto = Contexto();

        contexto.Usuarios.Add(NuevoUsuario());

        new InterceptorAuditoria(new ActorFalso { EsPlataforma = true }).Registrar(contexto);

        // Su usuario_id vive en la central y no resuelve aqui: es justo lo que el
        // origen desambigua.
        Assert.Equal(OrigenesAuditoria.Plataforma, Bitacora(contexto).Origen);
    }

    private static ContextoEmpresa Contexto()
    {
        var opciones = new DbContextOptionsBuilder<ContextoEmpresa>();
        opciones.UsarPostgres(CadenaMuerta);

        return new ContextoEmpresa(opciones.Options);
    }

    private static Usuario NuevoUsuario() => new()
    {
        Correo = "operador@bajio.mx",
        Nombre = "Operador",
        HashContrasena = HashSecreto,
    };

    private static Auditoria Bitacora(ContextoEmpresa contexto)
        => contexto.ChangeTracker.Entries<Auditoria>().Single().Entity;

    private static string[] Claves(string json)
        => [.. JsonDocument.Parse(json).RootElement
            .EnumerateObject()
            .Select(p => p.Name)];

    private sealed class ActorFalso : IContextoAuditoria
    {
        public Guid CorrelacionId { get; } = Guid.CreateVersion7();

        public bool EstaEstablecido { get; init; } = true;

        public Guid? UsuarioId { get; init; } = Guid.CreateVersion7();

        public string? UsuarioCorreo { get; init; } = "quien@bajio.mx";

        public string[] Roles { get; init; } = ["administrador"];

        public bool EsPlataforma { get; init; }

        public IPAddress? Ip { get; init; } = IPAddress.Parse("189.203.10.4");

        public void Establecer(
            Guid? usuarioId, string? correo, string[] roles, bool esPlataforma, IPAddress? ip)
            => throw new NotSupportedException();
    }
}
