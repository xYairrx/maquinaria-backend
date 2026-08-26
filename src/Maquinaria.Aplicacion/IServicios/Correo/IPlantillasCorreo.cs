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
    /// Misma idea que la de invitacion —el slug prellenado en el subdominio— pero a
    /// otra pantalla: quien restablece ya tiene cuenta y no viene a activarla.
    /// </summary>
    string LigaDeRestablecimiento(string slug, string tokenEnClaro);

    /// <summary>
    /// El correo del restablecimiento.
    ///
    /// SE MANDA SOLO CUANDO HAY DESTINATARIO REAL. No existe una variante "alguien
    /// pidio restablecer y aqui no hay cuenta" para avisarle al dueno del buzon: ese
    /// correo le confirmaria a quien probo la direccion —desde el buzon del tercero, si
    /// se lo reenvia— que la direccion no esta dada de alta, que es la misma fuga que
    /// la respuesta HTTP evita.
    /// </summary>
    MensajeCorreo Restablecimiento(string para, string razonSocial, string liga);

    /// <summary>
    /// Si la liga se devuelve en la respuesta HTTP del alta. SOLO en desarrollo: en
    /// produccion, cualquiera con acceso al panel podria tomar la sesion del
    /// administrador de un cliente antes de que abra su correo.
    /// </summary>
    bool DevuelveLigaEnRespuesta { get; }
}
