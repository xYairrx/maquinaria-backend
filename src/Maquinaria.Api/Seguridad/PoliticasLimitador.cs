namespace Maquinaria.Api.Seguridad;

/// <summary>
/// Nombres de las politicas del limitador de intentos.
///
/// Estaban como constantes dentro de los dos archivos de endpoints, que es donde se
/// declaraban las rutas. Al pasar a controladores esos archivos desaparecen, y estas
/// cadenas las necesitan DOS lados que no se conocen entre si: Program.cs, que configura
/// cada politica con su cupo y su ventana, y el atributo
/// <c>[EnableRateLimiting]</c> de cada accion. Una cadena repetida en dos sitios que
/// tienen que coincidir exactamente es justo lo que merece una constante.
/// </summary>
internal static class PoliticasLimitador
{
    /// <summary>
    /// Acceso de empresa: login y refresco. Particiona por SLUG e IP, y poder hacerlo es
    /// la razon de que el slug vaya en la ruta y no en el cuerpo — el limitador corre
    /// antes de que el cuerpo se lea.
    /// </summary>
    public const string AccesoEmpresa = "acceso-empresa";

    /// <summary>
    /// Limite propio para pedir un restablecimiento, mas estricto que
    /// <see cref="AccesoEmpresa"/>.
    ///
    /// Es el unico endpoint anonimo que MANDA CORREO, y eso lo vuelve un vector de abuso
    /// distinto del login: cada intento le llega al buzon de un tercero y gasta cuota de
    /// Resend. Diez por minuto —lo que permite la politica de acceso— son diez correos por
    /// minuto a la misma persona.
    /// </summary>
    public const string RestablecimientoEmpresa = "restablecimiento-empresa";

    /// <summary>
    /// Inicio de sesion de plataforma. Particiona solo por IP: no hay slug del que colgar
    /// una particion mas fina, y el correo vive en el cuerpo, que todavia no se ha leido.
    /// </summary>
    public const string InicioSesionPlataforma = "inicio-sesion";
}
