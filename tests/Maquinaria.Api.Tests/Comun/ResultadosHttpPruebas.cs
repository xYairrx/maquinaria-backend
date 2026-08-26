using Maquinaria.Api.Comun;
using Maquinaria.Aplicacion.Comun;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Maquinaria.Api.Tests.Comun;

/// <summary>
/// La traduccion de <see cref="RazonRechazo"/> a codigo HTTP.
///
/// Es una prueba de tres lineas por caso y vigila algo que se rompe callado: si un dia
/// Conflicto empieza a contestar 400, el frontend sigue funcionando —muestra el mensaje— y
/// nadie se entera de que perdio la capacidad de distinguir «no existe» de «choca».
/// </summary>
public class ResultadosHttpPruebas
{
    /// <summary>
    /// <c>ControllerBase.Problem()</c> resuelve su fabrica de ProblemDetails del contenedor
    /// de la peticion, que aqui no existe. Con esta se prueba la decision —el codigo— sin
    /// levantar un host.
    /// </summary>
    private sealed class FabricaProblemas : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext, int? statusCode = null, string? title = null,
            string? type = null, string? detail = null, string? instance = null)
            => new() { Status = statusCode, Title = title, Detail = detail };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelStateDictionary,
            int? statusCode = null, string? title = null, string? type = null,
            string? detail = null, string? instance = null)
            => new(modelStateDictionary) { Status = statusCode, Title = title, Detail = detail };
    }

    private sealed class ControladorDePrueba : ControllerBase;

    private static ControllerBase Controlador() => new ControladorDePrueba
    {
        ProblemDetailsFactory = new FabricaProblemas(),
    };

    private static int? Codigo(IActionResult resultado) => resultado switch
    {
        ObjectResult objeto => objeto.StatusCode ?? (objeto.Value as ProblemDetails)?.Status,
        StatusCodeResult codigo => codigo.StatusCode,
        _ => null,
    };

    [Fact]
    public void Ok_sin_ruta_es_200()
    {
        var resultado = Controlador().AHttp(Resultado<string>.Ok("listo"));

        Assert.Equal(StatusCodes.Status200OK, Codigo(resultado));
    }

    /// <summary>
    /// Con ruta es 201 y la cabecera Location la arma la funcion, no una cadena fija: el id
    /// no existe hasta que la operacion termino.
    /// </summary>
    [Fact]
    public void Ok_con_ruta_es_201_con_Location()
    {
        var resultado = Controlador()
            .AHttp(Resultado<string>.Ok("abc"), v => $"/api/cosas/{v}");

        var creado = Assert.IsType<CreatedResult>(resultado);

        Assert.Equal(StatusCodes.Status201Created, creado.StatusCode);
        Assert.Equal("/api/cosas/abc", creado.Location);
    }

    [Theory]
    [InlineData(RazonRechazo.Invalido, StatusCodes.Status400BadRequest)]
    [InlineData(RazonRechazo.NoEncontrado, StatusCodes.Status404NotFound)]
    [InlineData(RazonRechazo.Conflicto, StatusCodes.Status409Conflict)]
    public void Cada_razon_tiene_su_codigo(RazonRechazo razon, int esperado)
    {
        var rechazo = razon switch
        {
            RazonRechazo.NoEncontrado => Resultado<string>.NoEncontrado("no esta"),
            RazonRechazo.Conflicto => Resultado<string>.Conflicto("choca"),
            _ => Resultado<string>.Invalido("mal capturado"),
        };

        Assert.Equal(esperado, Codigo(Controlador().AHttp(rechazo)));
    }

    /// <summary>
    /// El motivo del rechazo llega al cliente: es el unico texto util de la respuesta, y lo
    /// escribe el Servicio que rechazo.
    /// </summary>
    [Fact]
    public void El_motivo_viaja_en_el_detalle()
    {
        var resultado = Controlador()
            .AHttp(Resultado<string>.Conflicto("Ya existe la marca 'Caterpillar'."));

        var objeto = Assert.IsType<ObjectResult>(resultado);
        var problema = Assert.IsType<ProblemDetails>(objeto.Value);

        Assert.Equal("Ya existe la marca 'Caterpillar'.", problema.Detail);
    }

    /// <summary>
    /// El resultado SIN valor exitoso es 204 y no un 200 con cuerpo nulo: no hay nada que
    /// devolver, y un 200 con null obliga al cliente a distinguir «vacio» de «no aplica».
    /// </summary>
    [Fact]
    public void Resultado_sin_valor_correcto_es_204()
        => Assert.Equal(
            StatusCodes.Status204NoContent, Codigo(Controlador().AHttp(Resultado.Ok())));

    /// <summary>
    /// Un <c>default(Resultado)</c> —incorrecto y sin razon— sale como 400 y no como 500.
    /// Solo puede venir de un descuido, y tratarlo como error del cliente es mas seguro que
    /// dejarlo escalar a excepcion.
    /// </summary>
    [Fact]
    public void Rechazo_sin_razon_es_400()
        => Assert.Equal(
            StatusCodes.Status400BadRequest, Codigo(Controlador().AHttp(default(Resultado))));
}
