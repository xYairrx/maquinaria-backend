using System.Net;
using Maquinaria.Aplicacion.Correo;
using Microsoft.Extensions.Options;

namespace Maquinaria.Infraestructura.Correo;

internal sealed class PlantillasCorreoWeb(IOptions<OpcionesCorreo> opciones) : IPlantillasCorreo
{
    public bool DevuelveLigaEnRespuesta => opciones.Value.DevolverLigaEnRespuesta;

    public string LigaDeInvitacion(string slug, string tokenEnClaro)
    {
        var baseUrl = opciones.Value.UrlBaseAplicacion.TrimEnd('/');

        // El slug va PRELLENADO en la liga. Es lo que resuelve que la persona no tenga
        // que recordar el identificador de su empresa la primera vez que entra.
        return $"{baseUrl}/invitacion"
            + $"?empresa={Uri.EscapeDataString(slug)}"
            + $"&token={Uri.EscapeDataString(tokenEnClaro)}";
    }

    public MensajeCorreo Invitacion(string para, string razonSocial, string liga)
    {
        var dias = opciones.Value.DiasVigenciaInvitacion;

        // WebUtility.HtmlEncode sobre todo lo que venga de datos: la razon social la
        // captura un superadministrador, pero un correo con HTML sin escapar es una
        // inyeccion esperando a pasar.
        var empresa = WebUtility.HtmlEncode(razonSocial);
        var ligaHtml = WebUtility.HtmlEncode(liga);

        var html = $"""
            <p>Hola,</p>
            <p>Se creo el acceso de <strong>{empresa}</strong> al sistema de maquinaria.</p>
            <p>Para definir tu contrasena y entrar, abre esta liga:</p>
            <p><a href="{ligaHtml}">{ligaHtml}</a></p>
            <p>La liga sirve una sola vez y caduca en {dias} dias.</p>
            <p>Si no esperabas este correo, ignoralo.</p>
            """;

        var texto = $"""
            Hola,

            Se creo el acceso de {razonSocial} al sistema de maquinaria.

            Para definir tu contrasena y entrar, abre esta liga:
            {liga}

            La liga sirve una sola vez y caduca en {dias} dias.

            Si no esperabas este correo, ignoralo.
            """;

        return new MensajeCorreo(para, $"Tu acceso a {razonSocial}", html, texto);
    }
}
