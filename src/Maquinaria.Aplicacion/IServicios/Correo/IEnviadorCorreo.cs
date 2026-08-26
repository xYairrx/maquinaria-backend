namespace Maquinaria.Aplicacion.Correo;

/// <summary>
/// Envia correo. La implementacion se elige por configuracion.
///
/// Existe por la misma razon que IAlmacenamientoArchivos: un cliente on-premise usara
/// su propio SMTP, no el servicio que usemos nosotros. En la nube resuelve a Resend;
/// en desarrollo, a una implementacion que escribe el mensaje en el log y no manda
/// nada.
///
/// EL ENVIO NUNCA DEBE HACER FALLAR LA OPERACION QUE LO PIDIO. Si el aprovisionamiento
/// de una empresa creo la base, la migro, sembro los roles y creo al administrador, que
/// el correo no salga no puede convertir todo eso en un fracaso. Por eso el resultado
/// se devuelve en lugar de lanzarse.
/// </summary>
public interface IEnviadorCorreo
{
    Task<ResultadoEnvio> EnviarAsync(MensajeCorreo mensaje, CancellationToken ct);
}

/// <param name="Enviado">Falso si no salio. La operacion que lo pidio decide que hacer.</param>
/// <param name="Detalle">Motivo cuando fallo, o el identificador del proveedor cuando salio.</param>
public readonly record struct ResultadoEnvio(bool Enviado, string? Detalle)
{
    public static ResultadoEnvio Ok(string? id = null) => new(true, id);

    public static ResultadoEnvio Fallo(string motivo) => new(false, motivo);
}
