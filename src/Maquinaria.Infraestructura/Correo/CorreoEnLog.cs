using Maquinaria.Aplicacion.Correo;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Infraestructura.Correo;

/// <summary>
/// Escribe el correo en el log y no manda nada. La implementacion de desarrollo, el
/// equivalente de AlmacenamientoDisco para los archivos.
///
/// Existe para poder cerrar el ciclo completo del criterio de salida de la Fase 0 —dar
/// de alta una empresa, invitar al administrador, que entre— SIN registrar un dominio
/// ni verificarlo en Resend.
/// </summary>
internal sealed class CorreoEnLog(ILogger<CorreoEnLog> log) : IEnviadorCorreo
{
    public Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
    {
        // El cuerpo de TEXTO, no el HTML: es el que se puede leer en una consola. Y
        // lleva la liga completa, que es lo unico que se necesita para continuar.
        log.LogWarning(
            "CORREO NO ENVIADO (proveedor 'log'). Para: {Para} | Asunto: {Asunto}\n{Cuerpo}",
            mensaje.Para,
            mensaje.Asunto,
            mensaje.CuerpoTexto);

        return Task.FromResult(ResultadoEnvio.Ok("log"));
    }
}
