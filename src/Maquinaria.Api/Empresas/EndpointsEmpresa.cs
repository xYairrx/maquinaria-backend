using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Api.Empresas;

/// <summary>
/// Lo que usan los usuarios de una empresa. Todo cuelga de /api/empresas/{slug}.
///
/// EL SLUG VA EN LA RUTA Y NO EN EL CUERPO, y no es estetica: el limitador de intentos
/// de .NET corre ANTES de leer el cuerpo de la peticion, asi que con el slug en el
/// cuerpo era imposible particionar por empresa. En la ruta si.
/// </summary>
internal static class EndpointsEmpresa
{
    public const string PoliticaAcceso = "acceso-empresa";

    public static IEndpointRouteBuilder MapearAccesoEmpresa(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/empresas/{slug}")
            .WithTags("Empresa")
            .AllowAnonymous()
            .RequireRateLimiting(PoliticaAcceso);

        grupo.MapGet("/invitaciones/{token}", ConsultarInvitacionAsync)
            .WithName("ConsultarInvitacion")
            .WithSummary("Dice a quien va dirigida una liga de invitacion vigente.")
            .Produces<InvitacionVigente>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/invitaciones/{token}", AceptarInvitacionAsync)
            .WithName("AceptarInvitacion")
            .WithSummary("Define la contrasena y activa la cuenta.")
            .Produces<AceptacionAceptada>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        grupo.MapPost("/sesion", IniciarSesionAsync)
            .WithName("IniciarSesionEmpresa")
            .WithSummary("Inicia sesion con empresa, correo y contrasena.")
            .Produces<SesionEmpresa>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return rutas;
    }

    private static async Task<IResult> ConsultarInvitacionAsync(
        string slug, string token, Invitaciones caso, CancellationToken ct)
    {
        var invitacion = await caso.ConsultarAsync(slug, token, ct);

        // 404 para TODOS los motivos: empresa inexistente, token invalido, usado,
        // invalidado o caducado. Distinguirlos le diria a cualquiera con una liga vieja
        // si la cuenta existe y en que estado esta.
        return invitacion is null
            ? Results.Problem(
                title: "Liga no valida",
                detail: "La liga no existe, ya se uso o caduco.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(invitacion.Value);
    }

    private static async Task<IResult> AceptarInvitacionAsync(
        string slug, string token, DefinirContrasena cuerpo,
        Invitaciones caso, CancellationToken ct)
    {
        var resultado = await caso.AceptarAsync(slug, token, cuerpo.Contrasena, ct);

        return resultado.Correcto
            ? Results.Ok(new AceptacionAceptada(resultado.Correo!, slug))
            : Results.Problem(
                title: "No se pudo activar la cuenta",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> IniciarSesionAsync(
        string slug, PeticionSesionEmpresa peticion, HttpContext contexto,
        IniciarSesionEmpresa caso, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peticion.Correo) || string.IsNullOrEmpty(peticion.Contrasena))
        {
            return Results.Problem(
                title: "Datos incompletos",
                detail: "Correo y contrasena son obligatorios.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var sesion = await caso.EjecutarAsync(
            slug,
            peticion,
            contexto.Connection.RemoteIpAddress?.ToString(),
            contexto.Request.Headers.UserAgent.ToString(),
            ct);

        // UN SOLO MENSAJE para las cuatro causas: la empresa no existe, no puede operar,
        // el correo no existe, o la contrasena no coincide.
        return sesion is null
            ? Results.Problem(
                title: "Credenciales incorrectas",
                detail: "Empresa, correo o contrasena incorrectos.",
                statusCode: StatusCodes.Status401Unauthorized)
            : Results.Ok(sesion.Value);
    }
}

public readonly record struct DefinirContrasena(string Contrasena);

public readonly record struct AceptacionAceptada(string Correo, string Empresa);
