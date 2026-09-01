using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Seguridad;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Empresas;

/// <param name="Correo">A quien va dirigida la liga. Se MUESTRA, no se pide.</param>
public readonly record struct InvitacionVigente(string Correo, string Nombre, string Empresa);

public readonly record struct ResultadoAceptacion(bool Correcto, string? Motivo, string? Correo)
{
    public static ResultadoAceptacion Exito(string correo) => new(true, null, correo);

    public static ResultadoAceptacion Rechazado(string motivo) => new(false, motivo, null);
}

/// <summary>
/// Los dos pasos de una invitacion: mirarla y usarla.
///
/// Juntos porque comparten la resolucion del tenant y la busqueda del token, y
/// separarlos duplicaria las dos cosas que mas importa hacer igual.
/// </summary>
public sealed class Invitaciones(
    IContextoTenant contextoTenant,
    Func<IUsuariosEmpresa> usuariosDe,
    IGeneradorTokens tokens,
    IHashContrasenas hash,
    ILogger<Invitaciones> log)
{
    /// <summary>
    /// PEREZOSO, y esto es lo importante. IUsuariosEmpresa depende de ContextoEmpresa,
    /// cuya cadena de conexion se resuelve leyendo el tenant. Si se inyectara directo,
    /// el contenedor construiria el contexto AL CREAR esta clase —antes de que nadie
    /// haya comprobado si hay tenant— y reventaria en las peticiones con slug
    /// desconocido, que son precisamente las que deben responder con el mensaje
    /// uniforme.
    ///
    /// Con la fabrica, la base solo se toca cuando ya se sabe que hay empresa.
    /// </summary>
    private IUsuariosEmpresa Usuarios => usuariosDe();

    /// <summary>
    /// Para pintar la pantalla: dice a quien va dirigida la liga, sin exigir sesion.
    ///
    /// Devuelve null para TODOS los motivos —empresa inexistente, token invalido, usado,
    /// invalidado o caducado— a proposito. Distinguirlos le diria a cualquiera con una
    /// liga vieja si la cuenta existe y en que estado esta.
    /// </summary>
    public async Task<InvitacionVigente?> ConsultarAsync(
        string slug, string tokenEnClaro, CancellationToken ct)
    {
        if (!contextoTenant.EstaResuelto || !contextoTenant.Actual.PuedeOperar)
        {
            return null;
        }

        var encontrado = await Usuarios.BuscarTokenVigenteAsync(
            tokens.Hashear(tokenEnClaro), PropositoToken.Invitacion, ct);

        return encontrado is null
            ? null
            : new InvitacionVigente(
                encontrado.Value.Usuario.Correo,
                encontrado.Value.Usuario.Nombre,
                contextoTenant.Actual.RazonSocial);
    }

    /// <summary>Define la contrasena y activa la cuenta.</summary>
    public async Task<ResultadoAceptacion> AceptarAsync(
        string slug, string tokenEnClaro, string contrasena, CancellationToken ct)
    {
        if (!PoliticaContrasena.EsValida(contrasena))
        {
            return ResultadoAceptacion.Rechazado(PoliticaContrasena.Explicacion);
        }

        if (!contextoTenant.EstaResuelto || !contextoTenant.Actual.PuedeOperar)
        {
            return ResultadoAceptacion.Rechazado("La liga no es valida o ya se uso.");
        }

        var encontrado = await Usuarios.BuscarTokenVigenteAsync(
            tokens.Hashear(tokenEnClaro), PropositoToken.Invitacion, ct);

        if (encontrado is null)
        {
            log.LogInformation("Intento de aceptar una invitacion no vigente en {Slug}.", slug);

            return ResultadoAceptacion.Rechazado("La liga no es valida o ya se uso.");
        }

        var (token, usuario) = encontrado.Value;

        await Usuarios.AceptarInvitacionAsync(usuario.Id, token.Id, hash.Hash(contrasena), ct);

        log.LogInformation(
            "Invitacion aceptada: {Correo} en {Slug} paso a Activo.", usuario.Correo, slug);

        return ResultadoAceptacion.Exito(usuario.Correo);
    }

}
