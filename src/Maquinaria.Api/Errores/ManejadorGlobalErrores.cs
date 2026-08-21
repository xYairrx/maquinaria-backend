using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Errores;

/// <summary>
/// Convierte cualquier excepcion no atrapada en un ProblemDetails.
///
/// El punto NO es dar formato bonito: es que una excepcion nunca se filtre al cliente
/// con su mensaje ni su traza. Un mensaje de excepcion de Npgsql puede llevar el
/// nombre de la base, el del servidor y parte de la consulta, y eso es informacion
/// que no debe salir de aqui.
///
/// El detalle real va al log, junto con el identificador de correlacion, que es lo
/// unico que se le devuelve al cliente: con ese id, un reporte de "me salio un error"
/// se convierte en una busqueda exacta.
/// </summary>
internal sealed class ManejadorGlobalErrores(
    IProblemDetailsService problemas,
    ILogger<ManejadorGlobalErrores> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        var correlacion = contexto.TraceIdentifier;

        log.LogError(
            excepcion,
            "Error no controlado en {Metodo} {Ruta}. Correlacion {Correlacion}.",
            contexto.Request.Method,
            contexto.Request.Path,
            correlacion);

        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno",
                // Generico a proposito. El detalle esta en el log.
                Detail = "Ocurrio un error inesperado. Reporta el identificador de correlacion.",
                Extensions = { ["correlacion"] = correlacion },
            },
        });
    }
}
