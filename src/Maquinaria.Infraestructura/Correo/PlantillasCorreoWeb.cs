using System.Net;
using Maquinaria.Aplicacion.Correo;
using Maquinaria.Dominio.Seguridad;
using Microsoft.Extensions.Options;

namespace Maquinaria.Infraestructura.Correo;

internal sealed class PlantillasCorreoWeb(IOptions<OpcionesCorreo> opciones) : IPlantillasCorreo
{
    public bool DevuelveLigaEnRespuesta => opciones.Value.DevolverLigaEnRespuesta;

    public string LigaDeInvitacion(string slug, string tokenEnClaro)
        => Liga(slug, "/invitacion", tokenEnClaro);

    public string LigaDeRestablecimiento(string slug, string tokenEnClaro)
        => Liga(slug, "/restablecer", tokenEnClaro);

    /// <summary>
    /// EL SLUG VA EN EL SUBDOMINIO, no en la cadena de consulta: cada empresa vive en el
    /// suyo y es de ahi de donde el frontend saca a que empresa se entra. Una liga con
    /// ?empresa= llegaria al dominio pelado, donde esas pantallas no existen.
    ///
    /// Se construye con UriBuilder y no concatenando: asi el esquema y el puerto salen de
    /// UrlBaseAplicacion sin tener que interpretarlos, y en desarrollo
    /// http://localhost:4200 da http://bajio.localhost:4200 sin ningun caso especial.
    /// </summary>
    private string Liga(string slug, string ruta, string tokenEnClaro)
    {
        var constructor = new UriBuilder(opciones.Value.UrlBaseAplicacion)
        {
            Path = ruta,
            Query = $"token={Uri.EscapeDataString(tokenEnClaro)}",
        };

        constructor.Host = $"{slug}.{constructor.Host}";

        return constructor.Uri.ToString();
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

    public MensajeCorreo Restablecimiento(string para, string razonSocial, string liga)
    {
        var empresa = WebUtility.HtmlEncode(razonSocial);
        var ligaHtml = WebUtility.HtmlEncode(liga);

        // LA ULTIMA LINEA NO ES RELLENO. Este correo le puede llegar a alguien que no
        // pidio nada, porque el formulario no exige probar que el buzon es tuyo, y esa
        // persona necesita saber que ignorarlo basta: no hay nada que cancelar y su
        // contrasena actual sigue sirviendo hasta que alguien abra la liga.
        var html = $"""
            <p>Hola,</p>
            <p>Alguien pidio restablecer la contrasena de esta cuenta en
            <strong>{empresa}</strong>.</p>
            <p>Para definir una contrasena nueva, abre esta liga:</p>
            <p><a href="{ligaHtml}">{ligaHtml}</a></p>
            <p>La liga sirve una sola vez y caduca en {PoliticaRestablecimiento.VigenciaTexto}.</p>
            <p>Si no fuiste tu, ignora este correo: tu contrasena actual no cambia.</p>
            """;

        var texto = $"""
            Hola,

            Alguien pidio restablecer la contrasena de esta cuenta en {razonSocial}.

            Para definir una contrasena nueva, abre esta liga:
            {liga}

            La liga sirve una sola vez y caduca en {PoliticaRestablecimiento.VigenciaTexto}.

            Si no fuiste tu, ignora este correo: tu contrasena actual no cambia.
            """;

        return new MensajeCorreo(
            para, $"Restablece tu contrasena de {razonSocial}", html, texto);
    }
}
