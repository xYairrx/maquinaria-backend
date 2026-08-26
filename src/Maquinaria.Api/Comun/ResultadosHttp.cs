using Maquinaria.Aplicacion.Comun;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Comun;

/// <summary>
/// EL UNICO LUGAR del proyecto donde una <see cref="RazonRechazo"/> se convierte en codigo
/// HTTP.
///
/// Existe para que ningun controlador vuelva a escribir un <c>Problem(...)</c> a mano. Con
/// cincuenta endpoints y once modulos, el mismo rechazo contestado 400 en un modulo y 409 en
/// otro es cuestion de tiempo, y el frontend acaba con un <c>switch</c> por endpoint en lugar
/// de uno por codigo.
///
/// El mapeo es el que documenta <see cref="RazonRechazo"/>: Invalido → 400,
/// NoEncontrado → 404, Conflicto → 409.
/// </summary>
internal static class ResultadosHttp
{
    /// <summary>
    /// Traduce un resultado con valor.
    ///
    /// <paramref name="rutaCreado"/> es lo que distingue un 200 de un 201: si se pasa, el
    /// exito responde <c>201 Created</c> con la cabecera <c>Location</c> armada a partir del
    /// valor. Se recibe como funcion y no como cadena porque la ruta lleva el id, que no
    /// existe hasta que la operacion termino.
    /// </summary>
    public static IActionResult AHttp<T>(
        this ControllerBase controlador, Resultado<T> resultado, Func<T, string>? rutaCreado = null)
        => resultado switch
        {
            { Correcto: true } when rutaCreado is not null
                => controlador.Created(rutaCreado(resultado.Valor!), resultado.Valor),

            { Correcto: true } => controlador.Ok(resultado.Valor),

            _ => Rechazo(controlador, resultado.Razon, resultado.Motivo),
        };

    /// <summary>
    /// Traduce un resultado sin valor. El exito es <c>204 No Content</c> y no un 200 con
    /// cuerpo vacio: no hay nada que devolver, y un 200 con <c>null</c> obliga al cliente a
    /// distinguir «vacio» de «no aplica».
    /// </summary>
    public static IActionResult AHttp(this ControllerBase controlador, Resultado resultado)
        => resultado.Correcto
            ? controlador.NoContent()
            : Rechazo(controlador, resultado.Razon, resultado.Motivo);

    /// <summary>
    /// El titulo va en espanol y describe la CATEGORIA, no el caso: el texto util para la
    /// pantalla es el <c>detail</c>, que lo escribe el Servicio o el Proceso que rechazo.
    /// </summary>
    private static IActionResult Rechazo(
        ControllerBase controlador, RazonRechazo? razon, string? motivo)
        => razon switch
        {
            RazonRechazo.NoEncontrado => controlador.Problem(
                title: "No encontrado",
                detail: motivo,
                statusCode: StatusCodes.Status404NotFound),

            RazonRechazo.Conflicto => controlador.Problem(
                title: "Conflicto",
                detail: motivo,
                statusCode: StatusCodes.Status409Conflict),

            // Invalido y, por si acaso, un resultado incorrecto SIN razon: 400. Un rechazo
            // sin razon solo puede venir de un `default(Resultado)`, y contestarlo como
            // error del cliente es mas seguro que contestarlo como 500.
            _ => controlador.Problem(
                title: "Peticion rechazada",
                detail: motivo,
                statusCode: StatusCodes.Status400BadRequest),
        };
}
