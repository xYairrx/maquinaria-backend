using System.Text;
using System.Threading.RateLimiting;
using Maquinaria.Api.Arranque;
using Maquinaria.Api.Comandos;
using Maquinaria.Api.Empresas;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Plataforma;
using Maquinaria.Api.Salud;
using Maquinaria.Infraestructura;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Documento OpenAPI nativo de .NET 10 (sin Swashbuckle). Se expone en /openapi/v1.json,
// que es la fuente del cliente HTTP generado del frontend (npm run api:sync).
builder.Services.AddOpenApi();

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

    // El acceso de empresa se particiona por SLUG e IP. Poder hacerlo es la razon de
    // que el slug vaya en la ruta: aqui todavia no se ha leido el cuerpo.
    opciones.AddPolicy(EndpointsEmpresa.PoliticaAcceso, contexto =>
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
    opciones.AddPolicy(EndpointsEmpresa.PoliticaRestablecimiento, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: (contexto.Request.RouteValues["slug"]?.ToString() ?? "?")
                + "|" + (contexto.Connection.RemoteIpAddress?.ToString() ?? "?"),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
            }));

    opciones.AddPolicy(EndpointsPlataforma.PoliticaInicioSesion, contexto =>
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

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(PoliticasAutorizacion.Plataforma, politica => politica
        .RequireAuthenticatedUser()
        .RequireClaim(ProveedorTokensJwt.ClaimAmbito, ProveedorTokensJwt.AmbitoPlataforma))
    .AddPolicy(PoliticasAutorizacion.Empresa, politica => politica
        .RequireAuthenticatedUser()
        .RequireClaim(ProveedorTokensJwt.ClaimAmbito, ProveedorTokensJwt.AmbitoEmpresa));

var app = builder.Build();

// --------------------------------------------------------------- comandos ----
// `dotnet run --project src/Maquinaria.Api -- migrar-empresas` corre y termina.
//
// Va AQUI, antes de configurar el pipeline: el comando usa el mismo contenedor y la
// misma configuracion que la API —incluidas las dos cadenas de conexion, que viven en
// los user-secrets de este proyecto— pero no levanta ningun puerto ni siembra nada.
if (ComandoMigrarEmpresas.EstaSolicitado(args))
{
    return await ComandoMigrarEmpresas.EjecutarAsync(app.Services);
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

// Despues de autenticar —necesita los claims validados— y antes de cualquier cosa que
// abra la base de una empresa.
app.UsarResolucionDeTenant();

app.UseAuthorization();

app.MapHealthChecks("/salud");

app.MapearPlataforma();
app.MapearEmpresas();
app.MapearPlanes();
app.MapearSaludEsquemas();
app.MapearAccesoEmpresa();
app.MapearSesionEmpresa();

await app.SembrarSuperadminAsync();

app.Run();

// EXPLICITO Y NO IMPLICITO: en cuanto un `return` de la rama de comandos devuelve un
// entero, el punto de entrada es int y el compilador exige que TODOS los caminos
// devuelvan uno (CS0161). Este es el "arranco la API y termino bien".
return 0;

/// <summary>Nombres de las politicas de autorizacion, para no repetir cadenas.</summary>
internal static class PoliticasAutorizacion
{
    /// <summary>Exige un token cuyo ambito sea la plataforma, no una empresa.</summary>
    public const string Plataforma = "plataforma";

    /// <summary>Exige un token de empresa. Un token de plataforma NO sirve aqui.</summary>
    public const string Empresa = "empresa";
}
