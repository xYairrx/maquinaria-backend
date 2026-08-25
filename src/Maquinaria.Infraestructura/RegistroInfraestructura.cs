using Maquinaria.Aplicacion.Correo;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Infraestructura.Correo;
using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Infraestructura.Empresas;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Plataforma;
using Maquinaria.Infraestructura.Seguridad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Maquinaria.Infraestructura;

/// <summary>
/// El unico lugar donde se registran las implementaciones de infraestructura.
///
/// Existe para que Program.cs no tenga que conocer los tipos concretos: la API sabe
/// que hay un IHashContrasenas, no que por debajo es PBKDF2. Cambiar a Argon2id seria
/// una linea aqui y ni un cambio en la API.
/// </summary>
public static class RegistroInfraestructura
{
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection servicios, IConfiguration configuracion)
    {
        // Cadena POOLED: es el runtime. Las migraciones usan la directa, via
        // FabricaContextoCentral.
        servicios.AddDbContext<ContextoCentral>(opciones =>
            opciones.UsarPostgres(
                configuracion.GetConnectionString("Central")
                ?? throw new InvalidOperationException("Falta ConnectionStrings:Central.")));

        servicios.AddOptions<OpcionesJwt>()
            .Bind(configuracion.GetSection(OpcionesJwt.Seccion))
            .ValidateOnStart();

        // Singleton: no tiene estado y su hash senuelo se calcula una sola vez. Con
        // scoped, cada peticion pagaria 600 mil iteraciones solo por construirlo.
        servicios.AddSingleton<IHashContrasenas, HashContrasenasPbkdf2>();

        // Singleton tambien: valida la llave y construye las credenciales de firma
        // una vez. Si la llave falta o es corta, revienta al arrancar y no en la
        // primera peticion de login.
        servicios.AddSingleton<IProveedorTokens, ProveedorTokensJwt>();

        servicios.AddScoped<IUsuariosPlataforma, UsuariosPlataformaEf>();

        servicios.AddScoped<IniciarSesionPlataforma>();

        // ------------------------------------------------------------------
        // Multi-tenancy
        // ------------------------------------------------------------------
        servicios.AddOptions<OpcionesMultiTenancy>()
            .Bind(configuracion.GetSection(OpcionesMultiTenancy.Seccion));

        servicios.AddMemoryCache();

        // Singleton: solo lee configuracion y arma cadenas. No tiene estado por peticion.
        servicios.AddSingleton<FabricaConexionesEmpresa>();
        servicios.AddSingleton<ProveedorContextoEmpresa>();

        servicios.AddScoped<IContextoTenant, ContextoTenant>();
        servicios.AddScoped<IDirectorioTenants, DirectorioTenantsEf>();

        // ContextoEmpresa NO lleva cadena fija: la resuelve por peticion a partir del
        // tenant. El acceso a IContextoTenant.Actual LANZA si no hay tenant resuelto, y
        // eso es la garantia que importa: no existe una base por defecto a la que caer.
        servicios.AddDbContext<ContextoEmpresa>((sp, opciones) =>
        {
            var tenant = sp.GetRequiredService<IContextoTenant>().Actual;
            var fabrica = sp.GetRequiredService<FabricaConexionesEmpresa>();

            opciones.UsarPostgres(fabrica.CadenaDeAplicacion(tenant.NombreBd));
        });

        // ------------------------------------------------------------------
        // Aprovisionamiento
        // ------------------------------------------------------------------
        servicios.AddScoped<IRegistroTenants, RegistroTenantsEf>();

        // El catalogo comercial: los planes y los modulos que los definen. Separado del
        // registro de tenants porque es otra responsabilidad —administrar el catalogo, no
        // dar de alta empresas— y otro momento: se define una vez y se consulta mucho.
        servicios.AddScoped<ICatalogoPlanes, CatalogoPlanesEf>();
        servicios.AddScoped<CrearPlan>();
        servicios.AddScoped<IAprovisionadorBaseDatos, AprovisionadorBaseDatosNpgsql>();
        servicios.AddScoped<ISembradorAdministrador, SembradorAdministradorEf>();
        servicios.AddScoped<AprovisionarEmpresa>();

        // Migrar N bases y reportar quien quedo atras. El comando de linea de comandos
        // resuelve MigrarEmpresas de este mismo contenedor: no hay un segundo registro
        // para el camino sin peticion.
        servicios.AddScoped<MigrarEmpresas>();
        servicios.AddScoped<SaludEsquemas>();

        // ------------------------------------------------------------------
        // Acceso de usuarios de empresa
        // ------------------------------------------------------------------
        servicios.AddScoped<IUsuariosEmpresa, UsuariosEmpresaEf>();

        // Fabrica perezosa. Sin esto, el contenedor construiria ContextoEmpresa —y por
        // tanto resolveria el tenant— al inyectar cualquier caso de uso que lo use, aun
        // en las peticiones donde todavia no se sabe si hay empresa.
        servicios.AddScoped<Func<IUsuariosEmpresa>>(
            sp => sp.GetRequiredService<IUsuariosEmpresa>);
        servicios.AddScoped<Invitaciones>();
        servicios.AddScoped<IniciarSesionEmpresa>();
        servicios.AddScoped<SolicitarRestablecimiento>();
        servicios.AddScoped<Restablecimientos>();

        servicios.AddSingleton<IGeneradorTokens, GeneradorTokensAleatorios>();

        // ------------------------------------------------------------------
        // Correo
        // ------------------------------------------------------------------
        servicios.AddOptions<OpcionesCorreo>()
            .Bind(configuracion.GetSection(OpcionesCorreo.Seccion));

        servicios.AddOptions<OpcionesResend>()
            .Bind(configuracion.GetSection(OpcionesResend.Seccion));

        servicios.AddScoped<IPlantillasCorreo, PlantillasCorreoWeb>();

        // La implementacion se elige por configuracion, igual que
        // IAlmacenamientoArchivos: 'log' en desarrollo, 'resend' en la nube.
        var proveedorCorreo = configuracion[$"{OpcionesCorreo.Seccion}:Proveedor"] ?? "log";

        if (proveedorCorreo.Equals("resend", StringComparison.OrdinalIgnoreCase))
        {
            var resend = configuracion.GetSection(OpcionesResend.Seccion).Get<OpcionesResend>()
                ?? new OpcionesResend();

            if (string.IsNullOrWhiteSpace(resend.Llave))
            {
                throw new InvalidOperationException(
                    "Correo:Proveedor es 'resend' pero falta Resend:Llave. Va en secretos.");
            }

            // HttpClient tipado: IHttpClientFactory maneja el reciclado de conexiones,
            // que es lo que evita el agotamiento de sockets de un HttpClient nuevo por
            // llamada.
            servicios.AddHttpClient<IEnviadorCorreo, CorreoResend>(cliente =>
            {
                cliente.BaseAddress = new Uri(resend.UrlBase.TrimEnd('/') + "/");
                cliente.Timeout = TimeSpan.FromSeconds(resend.SegundosTimeout);
                cliente.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", resend.Llave);
            });
        }
        else
        {
            servicios.AddScoped<IEnviadorCorreo, CorreoEnLog>();
        }

        return servicios;
    }
}
