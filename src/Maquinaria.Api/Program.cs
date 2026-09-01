using System.Text;
using System.Threading.RateLimiting;
using Maquinaria.Api.Arranque;
using Maquinaria.Api.Empresas;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Salud;
using Maquinaria.Api.Seguridad;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Infraestructura;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Controladores MVC, no Minimal API. La razon de peso es la matriz de 108 permisos: con
// [RequierePermiso("equipos.crear")] la exigencia se lee en la firma del metodo, mientras
// que con Minimal API seria un IEndpointFilter que hay que recordar encadenar en cada uno
// de los ~50 endpoints de la Fase 1, y el que se olvide queda abierto sin que nada lo
// detecte. Decidido el 2026-08-26; ver docs/07-plan-fase1.md §2.
builder.Services.AddControllers();

// Documento OpenAPI nativo de .NET 10 (sin Swashbuckle). Se expone en /openapi/v1.json,
// que es la fuente del cliente HTTP generado del frontend (npm run api:sync).
// Con transformadores: AddOpenApi() pelado emitia los numericos como `integer|string`
// y los enums sin sus valores, y de ahi salian tipos debiles en el frontend generado.
// El detalle, con las cifras medidas, esta en EsquemaOpenApi.
builder.Services.AgregarOpenApiDelProducto();

// Base central, opciones de JWT, hashing, tokens y casos de uso. Program.cs no conoce
// ningun tipo concreto de infraestructura.
builder.Services.AgregarInfraestructura(builder.Configuration);

// ---------------------------------------------------------------- errores ----
// ProblemDetails para todo, incluidos los 401 y 404 que genera el propio framework.
// El frontend corre en otro origen —Angular en :4200, la API en :5123— asi que sin
// CORS el navegador bloquea toda llamada.
//
// Ya no es una lista de origenes: cada empresa vive en su propio subdominio, asi que
// el conjunto es abierto y crece con cada cliente. La decision de que se acepta esta
// en OrigenesPermitidos, con el porque de cada comprobacion.
var opcionesCors = builder.Configuration.GetSection(OpcionesCors.Seccion).Get<OpcionesCors>()
    ?? new OpcionesCors();

builder.Services.AddCors(opciones =>
    opciones.AddDefaultPolicy(politica => politica
        .SetIsOriginAllowed(origen => OrigenesPermitidos.EsPermitido(origen, opcionesCors))
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorGlobalErrores>();

// ------------------------------------------------------------------ salud ----
builder.Services.AddHealthChecks()
    .AddCheck<ComprobacionBaseCentral>("base-central");

// --------------------------------------------------------------- limitador ---
// La tercera regla anti-abuso del diseno de login: limite de intentos.
//
// Particiona por IP y no por correo porque el limitador corre ANTES de leer el
// cuerpo de la peticion. El limite por combinacion de correo —y de slug, cuando
// exista el login de empresa— tiene que vivir en el caso de uso, con estado, y
// todavia no esta.
builder.Services.AddRateLimiter(opciones =>
{
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // EL 429 SE CONTESTA CON CUERPO, y antes no. El limitador rechaza con el codigo pelado,
    // asi que el cliente no recibia ni un `ProblemDetails`: la pantalla acababa enseñando
    // «Error 429», que no le dice a nadie que espere un momento ni por que se le corto.
    //
    // El texto va AQUI y no en el frontend, por la misma regla que el resto de los mensajes
    // de error: el servidor los redacta y el cliente los muestra tal cual.
    opciones.OnRejected = async (contexto, ct) =>
    {
        // Cuanto falta para que la ventana se reabra. El limitador lo sabe y lo ofrece en
        // los metadatos; sin esta cabecera, «espera un momento» es un consejo sin numero.
        var espera = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var valor)
            ? (int)Math.Ceiling(valor.TotalSeconds)
            : 60;

        contexto.HttpContext.Response.Headers.RetryAfter = espera.ToString();

        await contexto.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc6585#section-4",
                Title = "Demasiados intentos",
                Status = StatusCodes.Status429TooManyRequests,
                Detail = $"Se hicieron demasiados intentos seguidos. Vuelve a intentarlo en "
                    + $"{espera} segundos.",

                // El codigo para traducir, y los segundos APARTE del texto: asi el cliente
                // arma la frase en su idioma en lugar de recibirla hecha en el nuestro.
                Extensions =
                {
                    ["codigo"] = CodigosProblema.DemasiadosIntentos,
                    ["segundos"] = espera,
                },
            },
            ct);
    };

    // El acceso de empresa se particiona por SLUG e IP. Poder hacerlo es la razon de
    // que el slug vaya en la ruta: aqui todavia no se ha leido el cuerpo.
    opciones.AddPolicy(PoliticasLimitador.AccesoEmpresa, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: (contexto.Request.RouteValues["slug"]?.ToString() ?? "?")
                + "|" + (contexto.Connection.RemoteIpAddress?.ToString() ?? "?"),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // Pedir un restablecimiento manda correo, y eso cambia a quien le cuesta el abuso:
    // no es un intento fallido contra nosotros, es un mensaje al buzon de un tercero y
    // un consumo de la cuota del proveedor. Cupo mas chico y ventana mas larga que
    // PoliticaAcceso, con la misma particion por slug e IP.
    opciones.AddPolicy(PoliticasLimitador.RestablecimientoEmpresa, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: (contexto.Request.RouteValues["slug"]?.ToString() ?? "?")
                + "|" + (contexto.Connection.RemoteIpAddress?.ToString() ?? "?"),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
            }));

    opciones.AddPolicy(PoliticasLimitador.InicioSesionPlataforma, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// ------------------------------------------------------------------- auth ----
var opcionesJwt = builder.Configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>()
    ?? throw new InvalidOperationException(
        $"Falta la seccion de configuracion {OpcionesJwt.Seccion}.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        // SIN MAPEO DE CLAIMS ENTRANTES.
        //
        // Por defecto JwtBearer traduce los nombres cortos y estandar del token a los
        // URIs de WS-Federation: sub pasa a ser
        // http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier, email y
        // name igual. El resultado es que el token dice "sub" y el codigo que lo lee no
        // lo encuentra por ese nombre — un 401 que parece de autorizacion y no lo es.
        //
        // Emitimos nombres cortos a proposito, asi que aqui se leen tal cual.
        opciones.MapInboundClaims = false;

        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            // Con MapInboundClaims apagado hay que decirle de que claim sale el nombre,
            // o User.Identity.Name queda nulo.
            NameClaimType = JwtRegisteredClaimNames.Name,

            ValidateIssuer = true,
            ValidIssuer = opcionesJwt.Emisor,

            // LAS DOS AUDIENCIAS SE ACEPTAN AQUI, pero cada endpoint exige la suya con
            // una politica de autorizacion. Sin esa segunda comprobacion, un token de
            // plataforma serviria en un endpoint de empresa: los firma la misma llave.
            ValidateAudience = true,
            ValidAudiences = [opcionesJwt.AudienciaPlataforma, opcionesJwt.AudienciaEmpresa],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(opcionesJwt.Llave)),

            ValidateLifetime = true,
            // Sin tolerancia de reloj: el emisor y el validador son el mismo proceso.
            // Los cinco minutos por defecto alargarian de mas la vida de un token.
            ClockSkew = TimeSpan.Zero,
        };
    });

var autorizacion = builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PoliticasAutorizacion.Plataforma, politica => politica
        .RequireAuthenticatedUser()
        .RequireClaim(ProveedorTokensJwt.ClaimAmbito, ProveedorTokensJwt.AmbitoPlataforma))
    .AddPolicy(PoliticasAutorizacion.Empresa, politica => politica
        .RequireAuthenticatedUser()
        .RequireClaim(ProveedorTokensJwt.ClaimAmbito, ProveedorTokensJwt.AmbitoEmpresa));

// UNA POLICY POR PERMISO, registradas en un bucle sobre las claves conocidas —modulos por
// acciones—. Son unas ciento treinta y su costo es un diccionario en memoria.
//
// El bucle en lugar de un IAuthorizationPolicyProvider dinamico: con esto, un
// [RequierePermiso("equipos.crearr")] revienta al llegar la peticion —«policy not found»—
// mientras que el provider dinamico aceptaria la cadena y devolveria 403 para siempre, en
// silencio, sobre un endpoint que nadie puede alcanzar.
//
// EL AMBITO VA DENTRO DE CADA POLICY, y no es redundante con el [Authorize(Empresa)] del
// controlador: asi un token de plataforma no satisface un permiso de empresa aunque alguien
// olvide poner la policy de ambito en la clase.
foreach (var clave in ClavesPermiso.Todas)
{
    autorizacion.AddPolicy(clave, politica => politica
        .RequireAuthenticatedUser()
        .RequireClaim(ProveedorTokensJwt.ClaimAmbito, ProveedorTokensJwt.AmbitoEmpresa)
        .AddRequirements(new RequisitoPermiso(clave)));
}

builder.Services.AddSingleton<IAuthorizationHandler, ManejadorPermiso>();

var app = builder.Build();

// ------------------------------------------------------------------ comandos --
// Antes de armar la tuberia HTTP: si se invoco un comando, se ejecuta y se sale.
//
// Corre con EL MISMO contenedor que la aplicacion, asi que usa la misma resolucion de
// conexiones. Un comando con su propio arranque seria un segundo camino de codigo que
// puede divergir del que corre en produccion.
if (args.Length > 0 && args[0] == ComandoMigrarEmpresas.Nombre)
{
    return await ComandoMigrarEmpresas.EjecutarAsync(app, args);
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// EN DESARROLLO NO SE REDIRIGE A HTTPS, y no es una relajacion caprichosa: rompia todas
// las llamadas del navegador.
//
// El sintoma era enganoso. El preflight OPTIONS salia por http://localhost:5123 y
// respondia 204 —CORS bien—, pero la peticion real se redirigia a https://localhost:7020
// y ahi el navegador cortaba con ERR_CERT_AUTHORITY_INVALID, porque el certificado de
// desarrollo no esta en el almacen de confianza. Angular solo ve un error de red, asi
// que la pantalla dice "no se pudo contactar al servidor" mientras la API responde
// perfectamente a curl y a PowerShell, que no validan el certificado igual.
//
// La alternativa es 'dotnet dev-certs https --trust' en cada maquina y apuntar el
// frontend al 7020. Es igual de valido y mas parecido a produccion, pero exige un paso
// manual por maquina que nada verifica: quien lo olvide pierde la tarde con un error que
// no habla de certificados.
//
// En produccion la redireccion sigue activa, que es donde importa.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();

app.UseAuthentication();

// Antes de la resolucion de tenant: el login de empresa escribe sesion_refresh en ese
// mismo camino, asi que el actor tiene que estar puesto antes de que nada guarde.
app.UsarContextoDeAuditoria();

// Despues de autenticar —necesita los claims validados— y antes de cualquier cosa que
// abra la base de una empresa.
app.UsarResolucionDeTenant();

app.UseAuthorization();

app.MapHealthChecks("/salud");

// Los nueve controladores de Controladores/. Un archivo nuevo ahi se descubre solo: ya no
// hay una lista de Mapear*() que haya que acordarse de ampliar —y de la que un endpoint
// nuevo podia quedarse fuera sin que nada avisara—.
app.MapControllers();

await app.SembrarSuperadminAsync();

app.Run();

return 0;
