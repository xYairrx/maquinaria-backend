namespace Maquinaria.Dominio.Seguridad;

/// <summary>
/// Cuanto vale una liga de restablecimiento.
///
/// NO ES CONFIGURACION, y la asimetria con la invitacion es deliberada. Los dias de
/// vigencia de una invitacion viven en OpcionesCorreo porque son comodidad operativa:
/// que una empresa quiera darle dos semanas a su gente para entrar no le quita
/// seguridad a nadie. La ventana de un restablecimiento es lo contrario —es el tiempo
/// durante el cual un correo interceptado abre una cuenta ajena— y dejarla en un
/// appsettings es dejar que alguien la suba a treinta dias sin darse cuenta de lo que
/// esta haciendo.
///
/// Una hora es lo que ya documenta <see cref="PropositoToken.RestablecerContrasena"/>,
/// y este es el unico lugar donde ese numero existe.
/// </summary>
public static class PoliticaRestablecimiento
{
    public static TimeSpan Vigencia => TimeSpan.FromHours(1);

    /// <summary>
    /// Como se dice la vigencia en el correo. Vive junto al numero para que cambiar
    /// uno obligue a ver el otro: una plantilla que promete un plazo distinto del que
    /// aplica genera tickets de soporte, no errores de compilacion.
    /// </summary>
    public const string VigenciaTexto = "una hora";
}
