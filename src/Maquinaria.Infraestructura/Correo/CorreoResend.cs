using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Maquinaria.Aplicacion.Correo;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maquinaria.Infraestructura.Correo;

/// <summary>
/// Envia por Resend, con un HttpClient tipado contra su API.
///
/// SIN PAQUETE DE NUGET, a proposito: la API de Resend que necesitamos es UN endpoint
/// —POST /emails— y un SDK de terceros para eso es una dependencia mas que pinear,
/// auditar y actualizar, a cambio de ahorrar veinte lineas. El mismo criterio que
/// descarto MediatR y un paquete de Argon2.
/// </summary>
internal sealed class CorreoResend : IEnviadorCorreo
{
    private readonly HttpClient _http;
    private readonly IOptions<OpcionesCorreo> _correo;
    private readonly ILogger<CorreoResend> _log;

    public CorreoResend(
        HttpClient http,
        IOptions<OpcionesCorreo> correo,
        IOptions<OpcionesResend> resend,
        ILogger<CorreoResend> log)
    {
        // AQUI y no al registrar los servicios: un comando que no manda correos no tiene
        // por que exigir la llave. Esto se construye la primera vez que alguien intenta
        // enviar, asi que el fallo sigue siendo temprano y claro.
        if (string.IsNullOrWhiteSpace(resend.Value.Llave))
        {
            throw new InvalidOperationException(
                "Correo:Proveedor es 'resend' pero falta Resend:Llave. Va en secretos.");
        }

        _http = http;
        _correo = correo;
        _log = log;
    }

    public async Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct)
    {
        var opciones = _correo.Value;

        var peticion = new PeticionResend(
            From: $"{opciones.NombreRemitente} <{opciones.Remitente}>",
            To: [mensaje.Para],
            Subject: mensaje.Asunto,
            Html: mensaje.CuerpoHtml,
            Text: mensaje.CuerpoTexto);

        try
        {
            var respuesta = await _http.PostAsJsonAsync("emails", peticion, ct);

            if (respuesta.IsSuccessStatusCode)
            {
                var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaResend>(ct);

                _log.LogInformation(
                    "Correo enviado por Resend a {Para}. Id {Id}.", mensaje.Para, cuerpo?.Id);

                return ResultadoEnvio.Ok(cuerpo?.Id);
            }

            // El cuerpo del error de Resend se lee para el LOG, no para la respuesta:
            // puede traer detalles de configuracion de la cuenta.
            var detalle = await respuesta.Content.ReadAsStringAsync(ct);

            _log.LogError(
                "Resend rechazo el envio a {Para}: {Codigo} {Detalle}",
                mensaje.Para, (int)respuesta.StatusCode, detalle);

            return ResultadoEnvio.Fallo($"Resend respondio {(int)respuesta.StatusCode}.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // NO se propaga. El envio es best-effort: que Resend este caido no puede
            // convertir un aprovisionamiento correcto en un fracaso.
            _log.LogError(e, "No se pudo contactar a Resend para enviar a {Para}.", mensaje.Para);

            return ResultadoEnvio.Fallo("No se pudo contactar al proveedor de correo.");
        }
    }

    // Los nombres de la API de Resend son en ingles y en minusculas; el JsonPropertyName
    // los fija en lugar de depender de la politica de nombres que este configurada.
    private sealed record PeticionResend(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);

    private sealed record RespuestaResend(
        [property: JsonPropertyName("id")] string? Id);
}
