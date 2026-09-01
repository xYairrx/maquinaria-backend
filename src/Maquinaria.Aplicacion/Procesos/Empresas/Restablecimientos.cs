using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Seguridad;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Los dos pasos de un restablecimiento ya solicitado: mirar si la liga sirve y usarla.
///
/// Juntos por lo mismo que en <see cref="Invitaciones"/>: comparten la resolucion del
/// tenant y la busqueda del token, y separarlos duplicaria las dos cosas que mas
/// importa hacer igual.
///
/// LA DIFERENCIA CON LA INVITACION QUE HAY QUE VER: alli el paso de consulta devuelve a
/// quien va dirigida la liga, porque la invitacion es el primer contacto y la pantalla
/// tiene que decir a que empresa y con que correo se esta entrando. Aqui NO se devuelve
/// nada de eso. Quien restablece ya conoce su cuenta, asi que mostrar el correo no le
/// aporta y en cambio convierte una liga adivinada o interceptada en una confirmacion
/// de que direccion existe en que empresa.
/// </summary>
public sealed class Restablecimientos(
    IContextoTenant contextoTenant,
    Func<IUsuariosEmpresa> usuariosDe,
    IGeneradorTokens tokens,
    IHashContrasenas hash,
    ILogger<Restablecimientos> log)
{
    /// <summary>Perezoso. Ver la nota en Invitaciones.</summary>
    private IUsuariosEmpresa Usuarios => usuariosDe();

    /// <summary>
    /// Si la liga sirve. UN SOLO BIT, nada mas.
    ///
    /// Existe para no pedirle a alguien una contrasena que se va a rechazar de todas
    /// formas: sin esto, la pantalla acepta doce caracteres, los confirma, y solo
    /// entonces se entera de que la liga caduco.
    /// </summary>
    public async Task<bool> EsUsableAsync(string slug, string tokenEnClaro, CancellationToken ct)
    {
        if (!contextoTenant.EstaResuelto || !contextoTenant.Actual.PuedeOperar)
        {
            return false;
        }

        var encontrado = await Usuarios.BuscarTokenVigenteAsync(
            tokens.Hashear(tokenEnClaro), PropositoToken.RestablecerContrasena, ct);

        return encontrado is not null
            && encontrado.Value.Usuario.Estado == EstadoUsuario.Activo;
    }

    /// <summary>
    /// Define la contrasena nueva, quema el token y cierra las sesiones abiertas.
    ///
    /// Se reusa ResultadoAceptacion: los tres desenlaces son los mismos —correcto, o
    /// rechazado con un motivo— y un gemelo con otro nombre seria duplicacion.
    /// </summary>
    public async Task<ResultadoAceptacion> RestablecerAsync(
        string slug, string tokenEnClaro, string contrasena, CancellationToken ct)
    {
        // La politica PRIMERO, antes de tocar la base. Una contrasena corta no debe
        // quemar la liga: obligaria a pedir otra por haberse equivocado escribiendo.
        if (!PoliticaContrasena.EsValida(contrasena))
        {
            return ResultadoAceptacion.Rechazado(PoliticaContrasena.Explicacion);
        }

        if (!contextoTenant.EstaResuelto || !contextoTenant.Actual.PuedeOperar)
        {
            return ResultadoAceptacion.Rechazado(MotivoUniforme);
        }

        // El proposito va en la busqueda: un token de invitacion NO abre esta puerta.
        // Para eso existe el enum, y esta linea es el lugar donde se cobra.
        var encontrado = await Usuarios.BuscarTokenVigenteAsync(
            tokens.Hashear(tokenEnClaro), PropositoToken.RestablecerContrasena, ct);

        if (encontrado is null)
        {
            // Un solo motivo para los cinco casos: no existe, es de otro proposito, ya se
            // uso, se invalido al pedir otra, o caduco.
            log.LogInformation(
                "Intento de restablecer con una liga no vigente en {Slug}.", slug);

            return ResultadoAceptacion.Rechazado(MotivoUniforme);
        }

        var (token, usuario) = encontrado.Value;

        // El estado se comprueba AL CONSUMIR y no solo al emitir: entre que se pidio la
        // liga y se abre puede haber pasado una hora, y suspender a alguien tiene que
        // surtir efecto sin esperar a que caduquen las ligas que ya tenia.
        if (usuario.Estado != EstadoUsuario.Activo)
        {
            log.LogWarning(
                "Liga de restablecimiento vigente de un usuario {Estado} en {Slug}.",
                usuario.Estado, slug);

            return ResultadoAceptacion.Rechazado(MotivoUniforme);
        }

        await Usuarios.RestablecerContrasenaAsync(
            usuario.Id, token.Id, hash.Hash(contrasena), ct);

        log.LogInformation(
            "Contrasena restablecida: {Correo} en {Slug}. Sesiones de refresco revocadas.",
            usuario.Correo, slug);

        return ResultadoAceptacion.Exito(usuario.Correo);
    }

    /// <summary>
    /// El unico motivo que se le dice a quien abre una liga que no sirve. Constante y no
    /// literal repetido: dos mensajes que se parecen pero no son iguales son, a efectos
    /// de quien los mide, dos mensajes distintos.
    /// </summary>
    private const string MotivoUniforme = "La liga no es valida, ya se uso o caduco.";
}
