namespace Maquinaria.Api.Arranque;

/// <summary>
/// Configuracion de CORS.
/// </summary>
internal sealed class OpcionesCors
{
    public const string Seccion = "Cors";

    /// <summary>
    /// Origenes exactos. Aqui van los de la plataforma —el panel de superadmin y la
    /// pantalla de seleccion de empresa—, que no son subdominios de cliente.
    /// </summary>
    public string[] Origenes { get; set; } = [];

    /// <summary>
    /// Dominio bajo el cual CUALQUIER subdominio es un origen valido: cada empresa
    /// vive en el suyo (bajio.ejemplo.com). Vacio desactiva la regla por completo.
    ///
    /// En desarrollo vale 'localhost', que habilita bajio.localhost:4200 sin tocar el
    /// archivo hosts: Chrome y Edge resuelven *.localhost a 127.0.0.1 de forma nativa.
    /// </summary>
    public string DominioBase { get; set; } = string.Empty;

    /// <summary>
    /// Si se exige https. Se apaga solo en desarrollo, donde el dev server es http.
    /// </summary>
    public bool ExigirHttps { get; set; } = true;
}

/// <summary>
/// Decide que origenes acepta CORS.
///
/// POR QUE UN PREDICADO Y NO UNA LISTA: con un subdominio por empresa la lista es
/// abierta y crece con cada cliente nuevo. Mantenerla en configuracion significaria
/// redesplegar la API cada vez que se da de alta una empresa.
///
/// POR QUE NO AllowAnyOrigin: deshabilita las credenciales y deja que cualquier sitio
/// llame a la API desde el navegador de un usuario con sesion abierta. SetIsOriginAllowed
/// si es compatible con AllowCredentials, que hara falta cuando el token de refresco
/// pase a cookie HttpOnly.
///
/// LO QUE ESTA COMPROBACION NO HACE: verificar que el subdominio sea una empresa real.
/// Seria una consulta a la base en cada preflight, y ademas delataria que slugs son
/// clientes —justo lo que evitan las reglas anti-enumeracion del login—. Que el tenant
/// exista lo resuelve la peticion, no el CORS.
/// </summary>
internal static class OrigenesPermitidos
{
    public static bool EsPermitido(string origen, OpcionesCors opciones)
    {
        if (string.IsNullOrWhiteSpace(origen))
        {
            return false;
        }

        // La lista exacta gana y no pasa por ninguna validacion de forma: es
        // configuracion nuestra, no entrada del exterior.
        if (opciones.Origenes.Contains(origen, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (opciones.DominioBase.Length == 0)
        {
            return false;
        }

        if (!Uri.TryCreate(origen, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps && (opciones.ExigirHttps || uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        var anfitrion = uri.Host;
        var dominio = opciones.DominioBase;

        // El dominio pelado se acepta —ahi vive la pantalla de seleccion de empresa— y
        // cualquier cosa bajo el.
        //
        // El punto del prefijo es lo que hace segura la comparacion: 'malo-ejemplo.com'
        // termina en '-ejemplo.com', no en '.ejemplo.com', asi que no pasa. Sin el punto,
        // un EndsWith("ejemplo.com") lo aceptaria y regalaria el CORS a un dominio ajeno.
        return anfitrion.Equals(dominio, StringComparison.OrdinalIgnoreCase)
            || anfitrion.EndsWith('.' + dominio, StringComparison.OrdinalIgnoreCase);
    }
}
