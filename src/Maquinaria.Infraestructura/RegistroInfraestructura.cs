using System.Text;
using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Correo;
using Maquinaria.Aplicacion.Comercio;
using Maquinaria.Aplicacion.Contratos;
using Maquinaria.Aplicacion.Cotizaciones;
using Maquinaria.Aplicacion.Procesos.Comercio;
using Maquinaria.Aplicacion.Rentas;
using Maquinaria.Aplicacion.Procesos.Rentas;
using Maquinaria.Aplicacion.Disponibilidad;
using Maquinaria.Aplicacion.Procesos.Disponibilidad;
using Maquinaria.Aplicacion.Organizacion;
using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Aplicacion.Equipos;
using Maquinaria.Aplicacion.Procesos.Equipos;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Aplicacion.Terceros;
using Maquinaria.Infraestructura.Archivos;
using Maquinaria.Infraestructura.Correo;
using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Infraestructura.Empresas;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Catalogos;
using Maquinaria.Infraestructura.Servicios.Comun;
using Maquinaria.Infraestructura.Servicios.Comercio;
using Maquinaria.Infraestructura.Servicios.Contratos;
using Maquinaria.Infraestructura.Servicios.Cotizaciones;
using Maquinaria.Infraestructura.Servicios.Rentas;
using Maquinaria.Infraestructura.Servicios.Organizacion;
using Maquinaria.Infraestructura.Servicios.Disponibilidad;
using Maquinaria.Infraestructura.Servicios.Equipos;
using Maquinaria.Infraestructura.Servicios.Terceros;
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

        // EL LARGO DE LA LLAVE SE VALIDA AQUI Y NO SOLO EN EL CONSTRUCTOR DE
        // ProveedorTokensJwt. La comprobacion del constructor sigue puesta —es la que
        // protege al tipo de que alguien lo construya a mano— pero llegaba tarde: el
        // proveedor es singleton y solo se inyecta en los dos casos de uso de login, asi
        // que una llave corta o vacia dejaba arrancar el contenedor, contestar /salud, y
        // reventaba en el PRIMER LOGIN. En un despliegue con la variable mal puesta eso
        // es un servicio que parece sano y no deja entrar a nadie.
        //
        // Con Validate + ValidateOnStart el proceso no levanta. Y no estorba a
        // 'migrar-empresas': ese camino crea un ambito de app.Services y nunca arranca el
        // host, asi que esta validacion no corre — el mismo criterio que se aplico al
        // correo, donde registrar servicios no debe exigir lo que ese arranque no usa.
        servicios.AddOptions<OpcionesJwt>()
            .Bind(configuracion.GetSection(OpcionesJwt.Seccion))
            .Validate(
                opciones => Encoding.UTF8.GetByteCount(opciones.Llave) >= 32,
                "Jwt:Llave debe tener al menos 32 bytes para HMAC-SHA256. Va en secretos "
                + "—user-secrets en desarrollo, Jwt__Llave en Railway—, nunca en "
                + "appsettings.json.")
            .ValidateOnStart();

        // Singleton: no tiene estado y su hash senuelo se calcula una sola vez. Con
        // scoped, cada peticion pagaria 600 mil iteraciones solo por construirlo.
        servicios.AddSingleton<IHashContrasenas, HashContrasenasPbkdf2>();

        // Singleton tambien: construye las credenciales de firma una vez. La llave ya
        // quedo validada arriba, al arrancar; la comprobacion de su constructor es la
        // segunda linea, para quien lo construya fuera del contenedor —las pruebas—.
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
        servicios.AddScoped<IMigradorEmpresas, MigradorEmpresasEf>();
        servicios.AddScoped<IAprovisionadorBaseDatos, AprovisionadorBaseDatosNpgsql>();
        servicios.AddScoped<ISembradorAdministrador, SembradorAdministradorEf>();
        servicios.AddScoped<AprovisionarEmpresa>();
        servicios.AddScoped<ReenviarInvitacion>();

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
        // Catalogos de la empresa — Fase 1
        // ------------------------------------------------------------------
        // Scoped porque cuelgan de ContextoEmpresa, que se construye por peticion con la
        // cadena de la empresa que resolvio el middleware. Un singleton aqui compartiria la
        // base de la primera empresa que entrara con todas las demas.
        servicios.AddScoped<IServicioCategoriasEquipo, ServicioCategoriasEquipoEf>();
        servicios.AddScoped<IServicioMarcas, ServicioMarcasEf>();
        servicios.AddScoped<IServicioTiposEquipo, ServicioTiposEquipoEf>();
        servicios.AddScoped<IServicioModelosEquipo, ServicioModelosEquipoEf>();
        servicios.AddScoped<IServicioTarifas, ServicioTarifasEf>();
        servicios.AddScoped<IServicioClausulas, ServicioClausulasEf>();
        servicios.AddScoped<IServicioPuestos, ServicioPuestosEf>();

        // ------------------------------------------------------------------
        // Organizacion y terceros — Fase 1
        // ------------------------------------------------------------------
        servicios.AddScoped<IServicioUbicaciones, ServicioUbicacionesEf>();
        servicios.AddScoped<IServicioTrabajadores, ServicioTrabajadoresEf>();
        servicios.AddScoped<IServicioClientes, ServicioClientesEf>();
        servicios.AddScoped<IServicioProveedores, ServicioProveedoresEf>();

        // ------------------------------------------------------------------
        // Equipos — Fase 1
        // ------------------------------------------------------------------
        servicios.AddScoped<IServicioEquipos, ServicioEquiposEf>();
        servicios.AddScoped<IServicioEquipoTarifas, ServicioEquipoTarifasEf>();
        servicios.AddScoped<IServicioDocumentosEquipo, ServicioDocumentosEquipoEf>();
        servicios.AddScoped<ProcesoSubirDocumentoEquipo>();
        servicios.AddScoped<ProcesoDocumentoEquipo>();

        // ------------------------------------------------------------------
        // Disponibilidad — Fase 1
        // ------------------------------------------------------------------
        // La transaccion que usan los Procesos. Scoped como el contexto que envuelve.
        servicios.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajoEf>();

        servicios.AddScoped<IServicioOcupacion, ServicioOcupacionEf>();
        servicios.AddScoped<IServicioTransferencias, ServicioTransferenciasEf>();
        servicios.AddScoped<ProcesoTraspasarEquipo>();

        // ------------------------------------------------------------------
        // Comercial — Fase 1
        // ------------------------------------------------------------------
        servicios.AddScoped<IFolios, ServicioFoliosEf>();
        servicios.AddScoped<IServicioCotizaciones, ServicioCotizacionesEf>();
        servicios.AddScoped<IServicioRentas, ServicioRentasEf>();

        // Los cinco Procesos de rentas: son el entregable de la fase.
        servicios.AddScoped<ProcesoConfirmarRenta>();
        servicios.AddScoped<ProcesoExtenderRenta>();
        servicios.AddScoped<ProcesoCerrarRenta>();
        servicios.AddScoped<ProcesoRentaDesdeCotizacion>();

        servicios.AddScoped<IServicioContratos, ServicioContratosEf>();

        servicios.AddScoped<IServicioOrdenesCompra, ServicioOrdenesCompraEf>();
        servicios.AddScoped<IServicioOrdenesVenta, ServicioOrdenesVentaEf>();
        servicios.AddScoped<ProcesoFinalizarOrdenCompra>();
        servicios.AddScoped<ProcesoFinalizarOrdenVenta>();

        // ------------------------------------------------------------------
        // Almacenamiento de archivos
        // ------------------------------------------------------------------
        servicios.AddOptions<OpcionesAlmacenamiento>()
            .Bind(configuracion.GetSection(OpcionesAlmacenamiento.Seccion));

        // Mismo patron que el correo: la implementacion se elige por configuracion. Scoped y
        // no singleton porque AlmacenamientoDisco necesita el tenant de la peticion para
        // prefijar la ruta — es justo lo que impide que un archivo se escape de su empresa.
        var proveedorArchivos =
            configuracion[$"{OpcionesAlmacenamiento.Seccion}:Proveedor"] ?? "disco";

        if (!proveedorArchivos.Equals("disco", StringComparison.OrdinalIgnoreCase))
        {
            // AlmacenamientoS3 no existe todavia. Se avisa al arrancar en lugar de fallar en
            // la primera subida, que es el mismo criterio del aviso de Resend.
            Console.Error.WriteLine(
                $"AVISO: Archivos:Proveedor es '{proveedorArchivos}' y solo existe 'disco'. "
                + "Se usa disco.");
        }

        servicios.AddScoped<IAlmacenamientoArchivos, AlmacenamientoDisco>();

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

            // NO se lanza aqui aunque falte la llave, y esa decision costo un error:
            // la version anterior reventaba al REGISTRAR los servicios, asi que
            // 'migrar-empresas' —un comando que no manda ni un correo— no podia arrancar
            // sin configurar el proveedor de correo. Registrar servicios no debe validar
            // lo que ese arranque en concreto no va a usar.
            //
            // La validacion vive ahora en el constructor de CorreoResend, que se
            // construye la primera vez que alguien intenta enviar. Se pierde el fallo al
            // arrancar y se gana que cada camino solo exija lo que necesita; el aviso de
            // abajo cubre el hueco.
            if (string.IsNullOrWhiteSpace(resend.Llave))
            {
                Console.Error.WriteLine(
                    "AVISO: Correo:Proveedor es 'resend' y falta Resend:Llave. "
                    + "El envio de correo va a fallar en cuanto se intente.");
            }

            // HttpClient tipado: IHttpClientFactory maneja el reciclado de conexiones,
            // que es lo que evita el agotamiento de sockets de un HttpClient nuevo por
            // llamada.
            servicios.AddHttpClient<IEnviadorCorreo, CorreoResend>(cliente =>
            {
                cliente.BaseAddress = new Uri(resend.UrlBase.TrimEnd('/') + "/");
                cliente.Timeout = TimeSpan.FromSeconds(resend.SegundosTimeout);
                // Solo si hay llave. Un `Bearer` vacio no autentica nada y ademas obliga
                // a este delegado a construir un encabezado con un parametro vacio, que
                // es la clase de detalle que revienta en un sitio sin relacion aparente.
                // Sin llave, `CorreoResend` devuelve un envio fallido y lo dice en el log.
                if (!string.IsNullOrWhiteSpace(resend.Llave))
                {
                    cliente.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue(
                            "Bearer", resend.Llave);
                }
            });
        }
        else
        {
            servicios.AddScoped<IEnviadorCorreo, CorreoEnLog>();
        }

        return servicios;
    }
}
