namespace Maquinaria.Aplicacion.Correo;

/// <summary>
/// Arma los correos del sistema. Vive detras de una interfaz porque las ligas dependen
/// de la URL publica del frontend, que es configuracion, no dominio.
/// </summary>
public interface IPlantillasCorreo
{
    /// <summary>
    /// La liga lleva el slug PRELLENADO. Es lo que resuelve la friccion de que la
    /// persona tenga que recordar el identificador de su empresa la primera vez.
    /// </summary>
    string LigaDeInvitacion(string slug, string tokenEnClaro);

    MensajeCorreo Invitacion(string para, string razonSocial, string liga);

    /// <summary>
    /// Si la liga se devuelve en la respuesta HTTP del alta. SOLO en desarrollo: en
    /// produccion, cualquiera con acceso al panel podria tomar la sesion del
    /// administrador de un cliente antes de que abra su correo.
    /// </summary>
    bool DevuelveLigaEnRespuesta { get; }
}
