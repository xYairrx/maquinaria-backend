namespace Maquinaria.Aplicacion.Correo;

/// <summary>
/// Un correo a enviar. Deliberadamente pobre: destinatario, asunto y dos cuerpos.
///
/// Lleva HTML y texto plano porque un correo transaccional con solo HTML acaba en spam
/// con mas frecuencia, y porque hay clientes que no renderizan HTML.
/// </summary>
/// <param name="Para">Un solo destinatario. No hay envio masivo en este sistema.</param>
public readonly record struct MensajeCorreo(
    string Para,
    string Asunto,
    string CuerpoHtml,
    string CuerpoTexto);
