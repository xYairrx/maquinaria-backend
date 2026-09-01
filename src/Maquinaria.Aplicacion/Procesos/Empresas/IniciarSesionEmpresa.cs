using System.Diagnostics;
using System.Net;
using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Seguridad;

// ALIAS Y NO EL ESPACIO DE NOMBRES ENTERO: Maquinaria.Dominio.Plataforma tambien tiene un
// `Usuario`, homonimo del de empresa a proposito —son la misma idea en dos mundos separados
// fisicamente— y traerlo aqui vuelve ambigua cada mencion. De ahi solo hace falta el enum.
using EstadoTenant = Maquinaria.Dominio.Plataforma.EstadoTenant;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Empresas;

public readonly record struct PeticionSesionEmpresa(string Correo, string Contrasena);

/// <summary>
/// El cuerpo del refresco. Un objeto y no la cadena suelta por lo mismo que
/// <c>CambioDeActivo</c>: la peticion se lee sola en un log y se le pueden agregar
/// campos sin cambiar la firma.
///
/// EL TOKEN VA EN EL CUERPO Y NO EN LA RUTA a proposito. Un token de refresco vive 30
/// dias, y las rutas se escriben en los logs de acceso de cualquier proxy que haya en
/// medio; el cuerpo de un POST, no.
/// </summary>
public readonly record struct PeticionRefresco(string TokenRefresco);

/// <param name="Permisos">
/// Vacio cuando <paramref name="AccesoTotal"/> es true: no hace falta enumerar 156
/// permisos para quien los salta todos.
/// </param>
public readonly record struct SesionEmpresa(
    string Token,
    DateTime ExpiraEn,
    string TokenRefresco,
    string Nombre,
    string Correo,
    string Empresa,
    bool AccesoTotal,
    IReadOnlyList<string> Permisos);

/// <summary>
/// Los tres desenlaces de un intento de inicio de sesion.
///
/// El tercero existe por una razon concreta: una empresa SUSPENDIDA contestaba «empresa,
/// correo o contrasena incorrectos», y quien tenia bien las tres cosas se quedaba pensando
/// que se habia equivocado de contrasena.
///
/// SOLO SE DICE DESPUES DE VALIDAR LAS CREDENCIALES, y ese orden es lo que impide que se
/// convierta en un enumerador de clientes: quien acerto correo y contrasena de esa empresa
/// ya sabia que existe, asi que decirselo no le regala nada. A quien solo prueba slugs le
/// sigue contestando el mensaje uniforme.
/// </summary>
/// <param name="ServicioDetenido">
/// El estado que impide operar —Suspendido o Cancelado— cuando las credenciales SI eran
/// correctas. Nulo en los otros dos desenlaces.
/// </param>
public readonly record struct ResultadoSesionEmpresa(
    SesionEmpresa? Sesion,
    EstadoTenant? ServicioDetenido)
{
    public static ResultadoSesionEmpresa Exito(SesionEmpresa sesion) => new(sesion, null);

    /// <summary>El mensaje uniforme: no existe, no coincide, o no esta activo.</summary>
    public static ResultadoSesionEmpresa Rechazado() => new(null, null);

    public static ResultadoSesionEmpresa Detenido(EstadoTenant estado) => new(null, estado);
}

/// <summary>
/// Las DOS formas de obtener una sesion de empresa: iniciarla con slug, correo y
/// contrasena, y renovarla con un token de refresco.
///
/// JUNTAS EN UNA CLASE por lo mismo que <see cref="Invitaciones"/> y
/// <see cref="Restablecimientos"/> agrupan sus dos pasos: comparten la resolucion del
/// tenant, LA COMPUERTA de permisos, la emision del JWT, la vigencia del refresco y la
/// forma de la respuesta. Separarlas duplicaria justo las cinco cosas que mas importa
/// hacer igual en los dos caminos — y la que un copy-paste dejaria atras es la
/// compuerta, con lo que un refresco devolveria permisos de modulos no contratados.
///
/// Tres reglas anti-filtracion, y las tres importan:
///
/// 1. UN SOLO MENSAJE de error. Nunca "esa empresa no existe" ni "el correo no existe".
///    Distinguirlos le regala a cualquiera la lista de clientes.
/// 2. TIEMPO CONSTANTE. Si la empresa no existe se responderia de inmediato y si existe
///    se tardaria ~130 ms hasheando. Esa diferencia es medible y revela que slugs son
///    clientes, asi que se gasta el mismo tiempo siempre.
/// 3. LIMITE DE INTENTOS, que vive en el endpoint porque el limitador de .NET corre
///    antes de leer el cuerpo — de ahi que el slug vaya en la RUTA.
/// </summary>
public sealed class IniciarSesionEmpresa(
    IContextoTenant contextoTenant,
    Func<IUsuariosEmpresa> usuariosDe,
    IHashContrasenas hash,
    IGeneradorTokens tokens,
    IProveedorTokens proveedor,
    ILogger<IniciarSesionEmpresa> log)
{
    /// <summary>
    /// Vigencia del token de refresco. La MISMA para el que emite el login y para el que
    /// emite cada rotacion: si el refresco alargara la vida de la cadena mas alla de esto
    /// no habria forma de que una sesion caducara nunca.
    ///
    /// Vive aqui y no en OpcionesJwt porque Aplicacion no depende de Infraestructura.
    /// OpcionesJwt.DiasRefresco existe y vale lo mismo; que difieran seria un error, no
    /// una decision.
    /// </summary>
    public const int DiasVigenciaRefresco = 30;

    /// <summary>
    /// Piso de tiempo de los RECHAZOS del refresco. El camino correcto no espera nada.
    ///
    /// Existe por la misma razon que el senuelo del login: sin el, un refresco contra un
    /// slug que no es cliente responde sin tocar ninguna base y uno contra un slug real
    /// paga al menos una consulta. Esa diferencia es medible y convierte este endpoint
    /// —que es anonimo— en el enumerador de clientes que el login y el restablecimiento
    /// se cuidan de no ser.
    ///
    /// Es MUCHO mas bajo que el de SolicitarRestablecimiento porque aqui el camino largo
    /// no manda correo: son una o dos consultas. Y solo se paga al rechazar, asi que el
    /// interceptor del frontend no arrastra este retardo en cada renovacion.
    ///
    /// Mismo limite conocido que el otro piso: si la base se degrada por encima de este
    /// numero, el relleno se agota y la diferencia vuelve a ser medible.
    /// </summary>
    public static readonly TimeSpan PisoDeRechazoRefresco = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// El unico motivo que se le dice a quien presenta un token de refresco que no
    /// sirve. Constante y no literal repetido, por lo mismo que en Restablecimientos.
    /// </summary>
    public const string MotivoRefrescoUniforme = "La sesion no es valida o expiro.";

    /// <summary>
    /// Perezoso: la base de la empresa no se toca hasta saber que la empresa existe.
    /// Ver la nota en Invitaciones.
    /// </summary>
    private IUsuariosEmpresa Usuarios => usuariosDe();

    public async Task<ResultadoSesionEmpresa> EjecutarAsync(
        string slug, PeticionSesionEmpresa peticion, string? ip, string? agente,
        CancellationToken ct)
    {
        var correo = peticion.Correo.Trim().ToLowerInvariant();

        // El middleware ya intento resolver por el slug de la ruta. Si no lo logro, la
        // empresa no existe o no puede operar — y el senuelo corre igual, porque sin el
        // la diferencia de tiempo delataria que slugs son clientes.
        if (!contextoTenant.EstaResuelto)
        {
            hash.VerificarSenuelo(peticion.Contrasena);
            log.LogInformation("Inicio de sesion rechazado para {Slug}/{Correo}.", slug, correo);
            return ResultadoSesionEmpresa.Rechazado();
        }

        var tenant = contextoTenant.Actual;

        var usuario = await Usuarios.BuscarPorCorreoAsync(correo, ct);

        // Solo Activo entra. Invitado —sin contrasena definida—, Suspendido y Baja, no.
        if (usuario is null
            || usuario.Estado != EstadoUsuario.Activo
            || usuario.HashContrasena is null)
        {
            hash.VerificarSenuelo(peticion.Contrasena);
            log.LogInformation("Inicio de sesion rechazado para {Slug}/{Correo}.", slug, correo);
            return ResultadoSesionEmpresa.Rechazado();
        }

        var verificacion = hash.Verificar(usuario.HashContrasena, peticion.Contrasena);

        if (!verificacion.EsValida)
        {
            log.LogInformation("Inicio de sesion rechazado para {Slug}/{Correo}.", slug, correo);
            return ResultadoSesionEmpresa.Rechazado();
        }

        // AQUI, Y NO ANTES. Las credenciales ya se comprobaron, asi que quien llega a esta
        // linea pertenece a la empresa y decirle por que no entra no le informa de nada que
        // no supiera. Puesto antes, seria un enumerador de clientes.
        //
        // No se abre sesion ni se registra acceso: no entra. Solo se le explica.
        if (!tenant.PuedeOperar)
        {
            log.LogInformation(
                "{Correo} acerto sus credenciales en {Slug}, pero la empresa esta en {Estado}.",
                correo, slug, tenant.Estado);

            return ResultadoSesionEmpresa.Detenido(tenant.Estado);
        }

        // El login exitoso es el UNICO momento en que tenemos la contrasena en claro, y
        // por tanto el unico en que se puede regenerar un hash con costo viejo.
        var hashNuevo = verificacion.NecesitaRehash ? hash.Hash(peticion.Contrasena) : null;

        await Usuarios.RegistrarAccesoAsync(usuario.Id, DateTime.UtcNow, hashNuevo, ct);

        var (accesoTotal, permisos, roles) = await ResolverCompuertaAsync(usuario, tenant, ct);

        // ---------- sesion de refresco ----------
        var refresco = tokens.Generar();

        await Usuarios.CrearSesionAsync(
            NuevaSesion(usuario.Id, refresco.Hash, ip, agente), ct);

        log.LogInformation("{Correo} inicio sesion en {Slug}.", correo, slug);

        return ResultadoSesionEmpresa.Exito(
            Emitir(usuario, tenant, accesoTotal, permisos, roles, refresco.EnClaro));
    }

    /// <summary>
    /// REFRESCO ROTATIVO: canjea un token de refresco por una sesion nueva y revoca el
    /// que se canjeo, en la misma operacion.
    ///
    /// Cinco decisiones, todas de seguridad:
    ///
    /// 1. EL TOKEN SE BUSCA POR SU HASH, no se compara en claro. Es el mismo patron de la
    ///    invitacion y del restablecimiento: lo guardado es el SHA-256, asi que la
    ///    comparacion la hace el motor sobre dos hashes de longitud fija y el valor en
    ///    claro no existe en la base. Una comparacion caracter a caracter del token
    ///    —o un LIKE— si tendria fuga de tiempo; esta no.
    /// 2. UN SOLO MOTIVO para inexistente, caducado, revocado, reusado, de un usuario que
    ///    ya no esta activo, o de una empresa que no puede operar. Y un solo TIEMPO, por
    ///    el piso de <see cref="PisoDeRechazoRefresco"/>.
    /// 3. DETECCION DE REUSO. Un token ya canjeado solo puede llegar de dos sitios: una
    ///    copia robada, o un cliente que perdio la respuesta de la rotacion anterior. No
    ///    se pueden distinguir, asi que se trata como robo y se revoca TODA la cadena del
    ///    usuario. El costo del falso positivo es un login; el del falso negativo es un
    ///    atacante con acceso indefinido.
    /// 4. LOS PERMISOS SE VUELVEN A RESOLVER, no se copian del token viejo. Es lo que
    ///    hace que revocar un permiso, cambiar un rol o retirar un modulo del plan surta
    ///    efecto en 15 minutos y no en 30 dias.
    /// 5. EL ESTADO DEL USUARIO SE COMPRUEBA AQUI. Suspender a alguien tiene que cortarle
    ///    el acceso sin esperar a que caduque su cadena de refresco.
    ///
    /// LIMITE CONOCIDO, para quien escriba el cliente: dos refrescos simultaneos con el
    /// MISMO token —dos pestanas, o un reintento automatico— hacen que el segundo llegue
    /// cuando el primero ya lo canjeo, y eso se lee como reuso y cierra la sesion. Es
    /// inherente a la rotacion sin ventana de gracia; el cliente tiene que serializar sus
    /// refrescos (un solo vuelo, los demas esperan al primero).
    /// </summary>
    public async Task<SesionEmpresa?> RefrescarAsync(
        string slug, PeticionRefresco peticion, string? ip, string? agente,
        CancellationToken ct)
    {
        var inicio = Stopwatch.GetTimestamp();

        var sesion = await TrabajarRefrescoAsync(slug, peticion, ip, agente, ct);

        if (sesion is null)
        {
            await EsperarAlPisoAsync(inicio);
        }

        return sesion;
    }

    private async Task<SesionEmpresa?> TrabajarRefrescoAsync(
        string slug, PeticionRefresco peticion, string? ip, string? agente,
        CancellationToken ct)
    {
        // IsNullOrWhiteSpace y no Trim() directo: el cuerpo lo deserializa el framework y
        // un JSON sin la propiedad deja la cadena en null pese al tipo no anulable.
        if (string.IsNullOrWhiteSpace(peticion.TokenRefresco)
            || !contextoTenant.EstaResuelto
            // Desde que el middleware resuelve tambien las suspendidas, esto hay
            // que decirlo aqui. El refresco se queda con su 401 uniforme: quien
            // lo reciba vuelve al login, y ahi si le explican por que.
            || !contextoTenant.Actual.PuedeOperar)
        {
            log.LogInformation("Refresco rechazado en {Slug}.", slug);
            return null;
        }

        var tenant = contextoTenant.Actual;

        var sesion = await Usuarios.BuscarSesionPorHashAsync(
            tokens.Hashear(peticion.TokenRefresco), ct);

        if (sesion is null)
        {
            log.LogInformation("Refresco rechazado en {Slug}.", slug);
            return null;
        }

        // ---------- DETECCION DE REUSO ----------
        // Se comprueba ANTES de RevocadoEn porque una sesion rotada tiene las dos marcas,
        // y de las dos esta es la que significa "alguien esta usando una copia".
        if (sesion.ReemplazadoPorId is not null)
        {
            log.LogWarning(
                "REUSO de un token de refresco ya canjeado en {Slug}, usuario {Usuario}. "
                + "Se revocan todas sus sesiones.",
                slug, sesion.UsuarioId);

            await Usuarios.RevocarSesionesDeAsync(sesion.UsuarioId, ct);

            return null;
        }

        if (sesion.RevocadoEn is not null || sesion.ExpiraEn <= DateTime.UtcNow)
        {
            log.LogInformation("Refresco rechazado en {Slug}.", slug);
            return null;
        }

        var usuario = await Usuarios.BuscarPorIdAsync(sesion.UsuarioId, ct);

        if (usuario is null || usuario.Estado != EstadoUsuario.Activo)
        {
            // La sesion es valida pero su dueno ya no puede entrar. Se revoca la cadena:
            // dejarla viva significaria reintentar este rechazo cada 15 minutos durante
            // 30 dias.
            log.LogInformation(
                "Refresco de un usuario {Estado} en {Slug}. Se revocan sus sesiones.",
                usuario?.Estado, slug);

            await Usuarios.RevocarSesionesDeAsync(sesion.UsuarioId, ct);

            return null;
        }

        var (accesoTotal, permisos, roles) = await ResolverCompuertaAsync(usuario, tenant, ct);

        var refresco = tokens.Generar();

        // La rotacion es UNA operacion: revoca el canjeado, lo enlaza con el nuevo y
        // guarda el nuevo. Si se hiciera en dos pasos y el segundo fallara, o quedarian
        // dos tokens vivos o ninguno.
        await Usuarios.RotarSesionAsync(
            sesion.Id, NuevaSesion(usuario.Id, refresco.Hash, ip, agente), ct);

        log.LogInformation("{Correo} refresco su sesion en {Slug}.", usuario.Correo, slug);

        return Emitir(usuario, tenant, accesoTotal, permisos, roles, refresco.EnClaro);
    }

    /// <summary>
    /// LA COMPUERTA: permisos del rol interseccion modulos del plan.
    ///
    /// UNA SOLA COPIA para el login y para el refresco. Si el refresco la reimplementara,
    /// el dia que se ajuste una se quedaria la otra atras, y "atras" aqui significa
    /// entregar permisos sobre modulos que la empresa no contrato.
    /// </summary>
    private async Task<(bool AccesoTotal, IReadOnlyList<string> Permisos,
        IReadOnlyList<string> Roles)> ResolverCompuertaAsync(
        Usuario usuario, TenantResuelto tenant, CancellationToken ct)
    {
        var suyos = await Usuarios.RolesDeAsync(usuario.Id, ct);

        // Ordenados para que el claim del token sea estable entre dos logins iguales.
        var roles = suyos
            .Select(r => r.Codigo)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        if (suyos.Any(r => r.AccesoTotal))
        {
            return (true, [], roles);
        }

        var delRol = await Usuarios.PermisosDeAsync(usuario.Id, ct);

        // Un permiso concedido sobre un modulo que el plan no incluye NO se otorga.
        // Si se dejara pasar, el permiso ganaria sobre lo contratado.
        var permisos = delRol
            .Where(p => tenant.IncluyeModulo(ModuloDe(p)))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        log.LogDebug(
            "{Correo}: {Efectivos} permisos efectivos de {Totales} del rol.",
            usuario.Correo, permisos.Count, delRol.Count);

        return (false, permisos, roles);
    }

    /// <summary>
    /// La fila de sesion_refresh, igual la emita el login o una rotacion. La vigencia
    /// sale de la constante, no de un literal por camino.
    /// </summary>
    private static SesionRefresh NuevaSesion(
        Guid usuarioId, string hashToken, string? ip, string? agente)
        => new()
        {
            UsuarioId = usuarioId,
            HashToken = hashToken,
            ExpiraEn = DateTime.UtcNow.AddDays(DiasVigenciaRefresco),
            Ip = ip is null ? null : IPAddress.TryParse(ip, out var dir) ? dir : null,
            AgenteUsuario = agente,
        };

    /// <summary>
    /// El JWT y la respuesta, identicos en los dos caminos. Que el refresco devuelva
    /// exactamente la misma forma que el login es lo que permite que el frontend tenga un
    /// solo contrato de sesion.
    /// </summary>
    private SesionEmpresa Emitir(
        Usuario usuario, TenantResuelto tenant, bool accesoTotal,
        IReadOnlyList<string> permisos, IReadOnlyList<string> roles,
        string tokenRefrescoEnClaro)
    {
        var emitido = proveedor.EmitirDeEmpresa(
            usuario.Id, usuario.Correo, usuario.Nombre, tenant.Id, tenant.Slug,
            accesoTotal, permisos, roles);

        return new SesionEmpresa(
            emitido.Token, emitido.ExpiraEn, tokenRefrescoEnClaro,
            usuario.Nombre, usuario.Correo, tenant.Slug, accesoTotal, permisos);
    }

    /// <summary>
    /// Rellena hasta el piso. Con CancellationToken.None a proposito, por lo mismo que en
    /// SolicitarRestablecimiento: una espera cancelable convertiria abortar la peticion en
    /// una forma de medir el tiempo real del trabajo, que es lo que el piso oculta.
    /// </summary>
    private static async Task EsperarAlPisoAsync(long inicio)
    {
        var restante = PisoDeRechazoRefresco - Stopwatch.GetElapsedTime(inicio);

        if (restante > TimeSpan.Zero)
        {
            await Task.Delay(restante, CancellationToken.None);
        }
    }

    /// <summary>
    /// La clave de un permiso es "modulo.accion", asi que el modulo es lo que va antes
    /// del primer punto. Ojo: hay claves de modulo con guion —"inspeccion-salida"— pero
    /// ninguna con punto, asi que partir por el primer punto es correcto.
    /// </summary>
    private static string ModuloDe(string clavePermiso)
    {
        var punto = clavePermiso.IndexOf('.');

        return punto < 0 ? clavePermiso : clavePermiso[..punto];
    }
}
