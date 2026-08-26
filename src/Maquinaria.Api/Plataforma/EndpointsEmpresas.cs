using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Api.Plataforma;

/// <summary>
/// Alta de empresas. Solo la plataforma: aqui se crean bases de datos.
/// </summary>
internal static class EndpointsEmpresas
{
    public static IEndpointRouteBuilder MapearEmpresas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/plataforma/empresas")
            .WithTags("Plataforma")
            .RequireAuthorization(PoliticasAutorizacion.Plataforma);

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarEmpresas")
            .WithSummary("Todas las empresas, con su estado de aprovisionamiento.")
            .Produces<IReadOnlyList<ResumenEmpresa>>();

        grupo.MapPost("/", AltaAsync)
            .WithName("DarDeAltaEmpresa")
            .WithSummary("Da de alta una empresa: crea y migra su base, y invita a su administrador.")
            .Produces<EmpresaAprovisionada>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        grupo.MapPost("/{slug}/reintento", ReintentarAsync)
            .WithName("ReintentarAltaEmpresa")
            .WithSummary("Reintenta un alta que quedo en Fallida. Solo desde ese estado.")
            .Produces<EmpresaAprovisionada>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // SIN CUERPO, y eso es la pieza de seguridad. Un campo de correo aqui reabriria la
        // escalada de privilegios que el reintento del alta tuvo: quien tenga acceso al panel
        // pediria la liga de una cuenta con acceso total a su propio buzon. El destinatario
        // sale de la base de la empresa y se DEVUELVE, para que quien lo dispara vea a donde
        // fue en lugar de preguntarselo.
        grupo.MapPost("/{slug}/invitacion", ReenviarInvitacionAsync)
            .WithName("ReenviarInvitacionEmpresa")
            .WithSummary("Reemite la invitacion del administrador. Solo si sigue Invitado.")
            .Produces<ResultadoReenvio>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return rutas;
    }

    private static async Task<IResult> ListarAsync(IRegistroTenants registro, CancellationToken ct)
        => Results.Ok(await registro.ListarAsync(ct));

    private static async Task<IResult> AltaAsync(
        AltaDeEmpresa alta, AprovisionarEmpresa caso, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alta.Slug)
            || string.IsNullOrWhiteSpace(alta.RazonSocial)
            || string.IsNullOrWhiteSpace(alta.CorreoAdministrador)
            || string.IsNullOrWhiteSpace(alta.NombreAdministrador))
        {
            return Results.Problem(
                title: "Datos incompletos",
                detail: "Slug, razon social, y correo y nombre del administrador son obligatorios.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var resultado = await caso.EjecutarAsync(alta, ct);

        if (resultado.Correcto)
        {
            var empresa = resultado.Empresa!.Value;

            return Results.Created($"/api/plataforma/empresas/{empresa.Slug}", empresa);
        }

        // Un rechazo por validacion es 400 y NO es un fallo del sistema: no debe
        // verse igual que un aprovisionamiento roto, ni en la respuesta ni en el log.
        return resultado.EsRechazo
            ? Results.Problem(
                title: "Alta rechazada",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest)
            : Results.Problem(
                title: "Aprovisionamiento incompleto",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Vuelve a correr los pasos 2 a 6 del aprovisionamiento sobre un tenant en Fallida.
    ///
    /// 200 y no 201: el tenant ya existia antes de esta llamada, asi que no se creo
    /// ningun recurso nuevo. El cuerpo es el mismo <see cref="EmpresaAprovisionada"/> del
    /// alta, porque lo que el panel necesita mostrar es lo mismo.
    /// </summary>
    private static async Task<IResult> ReintentarAsync(
        string slug, ReintentoDeAlta reintento, AprovisionarEmpresa caso, CancellationToken ct)
    {
        var resultado = await caso.ReintentarAsync(slug, reintento, ct);

        if (resultado.Correcto)
        {
            return Results.Ok(resultado.Empresa!.Value);
        }

        // ponytail: los tres rechazos —slug inexistente, estado distinto de Fallida y
        // registro inconsistente— salen todos como 400 con su motivo en el detalle, en
        // lugar de 404 y 409 por separado. Es un endpoint del panel de plataforma, ya
        // autenticado, y lo unico que hace la interfaz con la respuesta es mostrar el
        // texto; distinguir codigos no cambiaria una linea del frontend.
        return resultado.EsRechazo
            ? Results.Problem(
                title: "Reintento rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest)
            : Results.Problem(
                title: "Aprovisionamiento incompleto",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status500InternalServerError);
    }

    /// <summary>
    /// Reenvia la invitacion del administrador de una empresa.
    ///
    /// Existe porque el log del alta escribia «Hay que reenviarla» y no habia con que: el
    /// reintento solo acepta empresas en `Fallida`, y una empresa cuya base se creo bien pero
    /// cuyo correo no salio NO esta fallida. La unica salida era borrarla y volver a crearla.
    ///
    /// UN ENVIO QUE FALLA NO ES UN RECHAZO. Devuelve 200 con `invitacionEnviada: false`,
    /// porque la invitacion SI se reemitio —y la anterior ya quedo invalidada—: lo que falta
    /// es volver a intentar el correo, no repetir la operacion. Contestar un error aqui haria
    /// creer que la liga vieja sigue sirviendo.
    /// </summary>
    private static async Task<IResult> ReenviarInvitacionAsync(
        string slug, ReenviarInvitacion caso, CancellationToken ct)
    {
        var resultado = await caso.EjecutarAsync(slug, ct);

        // ponytail: los cuatro rechazos —slug mal formado, empresa inexistente,
        // aprovisionamiento a medias y administrador que ya no esta Invitado— salen como 400
        // con su motivo, por lo mismo que el reintento: es un endpoint del panel, ya
        // autenticado, y la interfaz solo muestra el texto.
        return resultado.Correcto
            ? Results.Ok(resultado)
            : Results.Problem(
                title: "Reenvio rechazado",
                detail: resultado.Motivo,
                statusCode: StatusCodes.Status400BadRequest);
    }
}
