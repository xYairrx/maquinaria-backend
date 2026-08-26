using Maquinaria.Aplicacion.Correo;
using Maquinaria.Dominio.Plataforma;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Empresas;

/// <param name="Correo">
/// A donde SE MANDO, y se devuelve a proposito.
///
/// Es la confirmacion de que el destinatario salio de la base y no de lo que capturo quien
/// disparo el reenvio — que es justo la puerta que este flujo tiene cerrada. Sin ensenarlo,
/// la pregunta «¿a que correo fue?» se queda sin responder y alguien acaba buscando un
/// campo donde escribirlo.
/// </param>
/// <param name="LigaInvitacion">
/// Solo en desarrollo, con <c>Correo:DevolverLigaEnRespuesta</c>. En produccion, cualquiera
/// con acceso al panel podria tomar la cuenta del administrador de un cliente antes de que
/// abra su correo.
/// </param>
public readonly record struct ResultadoReenvio(
    bool Correcto,
    string? Motivo,
    string? Correo,
    bool InvitacionEnviada,
    string? LigaInvitacion)
{
    public static ResultadoReenvio Rechazado(string motivo) =>
        new(false, motivo, null, false, null);

    public static ResultadoReenvio Exito(string correo, bool enviada, string? liga) =>
        new(true, null, correo, enviada, liga);
}

/// <summary>
/// Vuelve a mandar la invitacion del administrador de una empresa.
///
/// EXISTE PORQUE EL ALTA LO PEDIA A GRITOS Y NO HABIA NADA. Cuando el correo no sale, el
/// log del aprovisionamiento escribe «Hay que reenviarla» — y no existia ningun camino para
/// hacerlo. El reintento del alta no sirve: solo acepta empresas en <c>Fallida</c>, y una
/// empresa cuya base se creo bien pero cuyo correo no salio NO esta fallida. La unica salida
/// era borrar la empresa y volver a crearla, o pescar la liga del log.
///
/// NO RECIBE CORREO. El destinatario lo decide la base, y esa ausencia es la pieza de
/// seguridad de todo esto: un parametro de correo aqui reabriria la escalada de privilegios
/// que el reintento del alta tuvo — pedir la liga de una cuenta con acceso total al buzon de
/// quien la pide, y definirle la contrasena.
/// </summary>
public sealed class ReenviarInvitacion(
    IRegistroTenants registro,
    ISembradorAdministrador sembrador,
    IPlantillasCorreo plantillas,
    IEnviadorCorreo correo,
    ILogger<ReenviarInvitacion> log)
{
    public async Task<ResultadoReenvio> EjecutarAsync(string slug, CancellationToken ct)
    {
        var normalizado = FormatoSlug.Normalizar(slug);

        // El formato primero, por lo mismo que en el alta: sin esto un slug mal formado
        // llega hasta la consulta y sale como un error generico en vez de decir que esta mal.
        if (!FormatoSlug.EsValido(normalizado))
        {
            return ResultadoReenvio.Rechazado(FormatoSlug.Explicacion);
        }

        var tenant = await registro.BuscarPorSlugAsync(normalizado, ct);

        if (tenant is null)
        {
            return ResultadoReenvio.Rechazado($"No existe una empresa '{normalizado}'.");
        }

        // Su base tiene que existir. En cualquier otro estado de aprovisionamiento, abrirla
        // daria un error de conexion o un «relation does not exist» que no dice nada de lo
        // que realmente pasa: lo que le falta a esa empresa es terminar su alta.
        if (tenant.EstadoAprovisionamiento != EstadoAprovisionamiento.Lista)
        {
            return ResultadoReenvio.Rechazado(
                $"El aprovisionamiento de '{normalizado}' esta en {tenant.EstadoAprovisionamiento}. "
                + "Termina el alta antes de reenviar la invitacion.");
        }

        var reemision = await sembrador.ReemitirInvitacionAsync(tenant.NombreBd, ct);

        if (!reemision.Correcto)
        {
            return ResultadoReenvio.Rechazado(reemision.Motivo!);
        }

        // LA INVITACION ANTERIOR YA QUEDO INVALIDADA, tambien cuando el envio falle. Es
        // deliberado y conviene saberlo: no se deja la liga vieja viva como respaldo, porque
        // entonces dos ligas distintas definirian la contrasena de la misma cuenta. Si el
        // correo no sale, se reenvia otra vez.
        var liga = plantillas.LigaDeInvitacion(normalizado, reemision.TokenEnClaro);
        var mensaje = plantillas.Invitacion(reemision.Correo, tenant.RazonSocial, liga);
        var envio = await correo.EnviarAsync(mensaje, ct);

        if (!envio.Enviado)
        {
            log.LogError(
                "Invitacion reemitida en {Slug} pero el correo NO salio: {Motivo}.",
                normalizado, envio.Detalle);
        }

        // Igual que el alta: se guarda como quedo, exito o fallo. Un reenvio que tampoco sale
        // tiene que dejar la empresa marcada para poder volver a intentarlo desde el panel.
        await registro.MarcarInvitacionEnviadaAsync(tenant.Id, envio.Enviado, ct);

        return ResultadoReenvio.Exito(
            reemision.Correo,
            envio.Enviado,
            plantillas.DevuelveLigaEnRespuesta ? liga : null);
    }
}
