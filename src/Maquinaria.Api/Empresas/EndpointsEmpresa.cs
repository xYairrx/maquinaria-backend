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

    /// <summary>
    /// Limite propio para pedir un restablecimiento, mas estricto que
    /// <see cref="PoliticaAcceso"/>.
    ///
    /// Es el unico endpoint anonimo que MANDA CORREO, y eso lo vuelve un vector de abuso
    /// distinto del login: cada intento le llega al buzon de un tercero y gasta cuota de
    /// Resend. Diez por minuto —lo que permite la politica del grupo— son diez correos
    /// por minuto a la misma persona.
    /// </summary>
    public const string PoliticaRestablecimiento = "restablecimiento-empresa";

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

        grupo.MapPost("/restablecimientos", SolicitarRestablecimientoAsync)
            // Sustituye a la del grupo, no se suma: el middleware del limitador lee UNA
            // politica de la metadata del endpoint, y la del endpoint se agrega despues
            // de la del grupo, asi que gana. Es lo que se quiere: la mas estricta.
            .RequireRateLimiting(PoliticaRestablecimiento)
            .WithName("SolicitarRestablecimiento")
            .WithSummary("Pide una liga para restablecer la contrasena.")
            .Produces<RestablecimientoSolicitado>(StatusCodes.Status202Accepted);

        grupo.MapGet("/restablecimientos/{token}", ConsultarRestablecimientoAsync)
            .WithName("ConsultarRestablecimiento")
            .WithSummary("Dice si una liga de restablecimiento todavia sirve.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        grupo.MapPost("/restablecimientos/{token}", RestablecerAsync)
            .WithName("Restablecer")
            .WithSummary("Define la contrasena nueva y cierra las sesiones abiertas.")
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

    /// <summary>
    /// UNA SOLA SALIDA, y por eso no hay ni un if aqui.
    ///
    /// El caso de uso no devuelve nada —no se puede ramificar sobre lo que no existe— y
    /// el cuerpo es una instancia unica y estatica, no un objeto armado por peticion: no
    /// hay forma de que se cuele una diferencia de contenido, de orden de propiedades ni
    /// de longitud entre "el correo existe" y "el correo no existe". El tiempo lo iguala
    /// el propio caso de uso.
    ///
    /// 202 y no 200: lo unico que se puede afirmar es que la peticion se acepto. Si el
    /// correo salio, a quien salio y si habia cuenta detras son exactamente los datos que
    /// no se pueden confirmar.
    /// </summary>
    private static async Task<IResult> SolicitarRestablecimientoAsync(
        string slug, PeticionRestablecimiento peticion,
        SolicitarRestablecimiento caso, CancellationToken ct)
    {
        await caso.EjecutarAsync(slug, peticion, ct);

        return Results.Json(RespuestaUnica, statusCode: StatusCodes.Status202Accepted);
    }

    /// <summary>
    /// Se construye una vez y se reusa. Ver el comentario de arriba: la respuesta tiene
    /// que ser identica byte a byte en los dos casos.
    /// </summary>
    private static readonly RestablecimientoSolicitado RespuestaUnica = new(
        "Si el correo corresponde a una cuenta activa, ahi llegara la liga para "
        + "restablecer la contrasena. Caduca en una hora.");

    private static async Task<IResult> ConsultarRestablecimientoAsync(
        string slug, string token, Restablecimientos caso, CancellationToken ct)
    {
        var usable = await caso.EsUsableAsync(slug, token, ct);

        // 204 sin cuerpo, no un objeto con los datos de la cuenta como en la invitacion.
        // Aqui la pantalla solo necesita saber si tiene sentido pedir la contrasena, y
        // cualquier dato de mas convierte una liga adivinada en una fuente de informacion.
        return usable
            ? Results.NoContent()
            : Results.Problem(
                title: "Liga no valida",
                detail: "La liga no existe, ya se uso o caduco.",
                statusCode: StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> RestablecerAsync(
        string slug, string token, DefinirContrasena cuerpo,
        Restablecimientos caso, CancellationToken ct)
    {
        var resultado = await caso.RestablecerAsync(slug, token, cuerpo.Contrasena, ct);

        return resultado.Correcto
            ? Results.Ok(new AceptacionAceptada(resultado.Correo!, slug))
            : Results.Problem(
                title: "No se pudo restablecer la contrasena",
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

/// <param name="Mensaje">
/// El mismo texto siempre. Esta redactado para ser cierto en los dos casos: no afirma
/// que se haya mandado nada, y le dice a quien si tiene cuenta que revise su buzon.
/// </param>
public readonly record struct RestablecimientoSolicitado(string Mensaje);

public readonly record struct AceptacionAceptada(string Correo, string Empresa);
