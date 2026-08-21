using System.Security.Claims;
using Maquinaria.Aplicacion.Plataforma;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Maquinaria.Api.Plataforma;

/// <summary>
/// Endpoints del panel de plataforma: lo que usamos nosotros, no los clientes.
/// </summary>
internal static class EndpointsPlataforma
{
    /// <summary>Politica del limitador de intentos de inicio de sesion.</summary>
    public const string PoliticaInicioSesion = "inicio-sesion";

    public static IEndpointRouteBuilder MapearPlataforma(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/plataforma")
            .WithTags("Plataforma");

        grupo.MapPost("/sesion", IniciarSesionAsync)
            .AllowAnonymous()
            .RequireRateLimiting(PoliticaInicioSesion)
            .WithName("IniciarSesionPlataforma")
            .WithSummary("Inicia sesion como superadministrador de la plataforma.")
            .Produces<SesionPlataforma>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Endpoint PROTEGIDO. Sirve dos cosas: le permite al frontend validar un token
        // guardado sin adivinar, y es la primera comprobacion de que la politica de
        // ambito hace su trabajo — un token de empresa no debe entrar aqui.
        grupo.MapGet("/sesion/actual", ObtenerSesionActual)
            .RequireAuthorization(PoliticasAutorizacion.Plataforma)
            .WithName("ObtenerSesionActualPlataforma")
            .WithSummary("Devuelve la identidad del superadministrador autenticado.")
            .Produces<IdentidadPlataforma>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return rutas;
    }

    private static IResult ObtenerSesionActual(ClaimsPrincipal quien)
    {
        // Los claims ya los valido JwtBearer: firma, emisor, audiencia y vigencia. Y la
        // politica ya exigio que el ambito sea plataforma. Aqui solo se leen.
        var id = quien.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var correo = quien.FindFirstValue(JwtRegisteredClaimNames.Email);
        var nombre = quien.FindFirstValue(JwtRegisteredClaimNames.Name);

        return id is null || correo is null || nombre is null
            ? Results.Problem(statusCode: StatusCodes.Status401Unauthorized)
            : Results.Ok(new IdentidadPlataforma(Guid.Parse(id), correo, nombre));
    }

    private static async Task<IResult> IniciarSesionAsync(
        PeticionInicioSesion peticion,
        IniciarSesionPlataforma caso,
        CancellationToken ct)
    {
        // Validacion minima antes de gastar 600 mil iteraciones: un cuerpo vacio no
        // merece el costo de un hash.
        if (string.IsNullOrWhiteSpace(peticion.Correo) || string.IsNullOrEmpty(peticion.Contrasena))
        {
            return Results.Problem(
                title: "Datos incompletos",
                detail: "Correo y contrasena son obligatorios.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var sesion = await caso.EjecutarAsync(peticion, ct);

        // UN SOLO MENSAJE para las tres causas posibles —el correo no existe, la
        // contrasena no coincide, la cuenta esta inactiva—. Distinguirlas le regalaria
        // a cualquiera la lista de quien tiene acceso a la plataforma.
        return sesion is null
            ? Results.Problem(
                title: "Credenciales incorrectas",
                detail: "Correo o contrasena incorrectos.",
                statusCode: StatusCodes.Status401Unauthorized)
            : Results.Ok(sesion.Value);
    }
}

/// <param name="Id">El sub del token.</param>
internal readonly record struct IdentidadPlataforma(Guid Id, string Correo, string Nombre);
