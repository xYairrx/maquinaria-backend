using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Plataforma;

/// <param name="Correo">Se normaliza a minusculas y sin espacios antes de buscar.</param>
public readonly record struct PeticionInicioSesion(string Correo, string Contrasena);

/// <param name="Token">JWT de audiencia de plataforma.</param>
public readonly record struct SesionPlataforma(string Token, DateTime ExpiraEn, string Nombre, string Correo);

/// <summary>
/// Login de un superadministrador. Es el unico camino de entrada al panel de
/// plataforma, y el que protege el alta de empresas.
///
/// Este caso de uso NO distingue entre "el correo no existe", "la contrasena no
/// coincide" y "la cuenta esta inactiva": devuelve null en los tres. Distinguirlos le
/// regalaria a cualquiera la lista de quien tiene acceso a la plataforma.
/// </summary>
public sealed class IniciarSesionPlataforma(
    IUsuariosPlataforma usuarios,
    IHashContrasenas hash,
    IProveedorTokens tokens,
    ILogger<IniciarSesionPlataforma> log)
{
    public async Task<SesionPlataforma?> EjecutarAsync(PeticionInicioSesion peticion, CancellationToken ct)
    {
        var correo = peticion.Correo.Trim().ToLowerInvariant();

        var usuario = await usuarios.BuscarPorCorreoAsync(correo, ct);

        // TIEMPO CONSTANTE. Si la cuenta no existe se gasta el mismo tiempo que si
        // existiera, hasheando contra un senuelo. Sin esto, la diferencia de
        // respuesta —inmediata contra ~200 ms— revela que correos son cuentas.
        if (usuario is null || !usuario.Activo)
        {
            hash.VerificarSenuelo(peticion.Contrasena);
            log.LogInformation("Inicio de sesion de plataforma rechazado para {Correo}.", correo);
            return null;
        }

        var verificacion = hash.Verificar(usuario.HashContrasena, peticion.Contrasena);

        if (!verificacion.EsValida)
        {
            log.LogInformation("Inicio de sesion de plataforma rechazado para {Correo}.", correo);
            return null;
        }

        // El login exitoso es el UNICO momento en que tenemos la contrasena en claro,
        // asi que es el unico momento en que se puede subir el costo de un hash viejo.
        var hashNuevo = verificacion.NecesitaRehash ? hash.Hash(peticion.Contrasena) : null;

        if (hashNuevo is not null)
        {
            log.LogInformation("Rehasheando la contrasena de {Correo} con el costo actual.", correo);
        }

        var ahora = DateTime.UtcNow;
        await usuarios.RegistrarAccesoAsync(usuario.Id, ahora, hashNuevo, ct);

        var token = tokens.EmitirDePlataforma(usuario.Id, usuario.Correo, usuario.Nombre);

        log.LogInformation("Superadministrador {Correo} inicio sesion.", correo);

        return new SesionPlataforma(token.Token, token.ExpiraEn, usuario.Nombre, usuario.Correo);
    }
}
