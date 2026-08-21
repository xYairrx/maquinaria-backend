using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Seguridad;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Empresas;

public readonly record struct PeticionSesionEmpresa(string Correo, string Contrasena);

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
/// Login de un usuario de empresa: slug, correo y contrasena.
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
    /// Perezoso: la base de la empresa no se toca hasta saber que la empresa existe.
    /// Ver la nota en Invitaciones.
    /// </summary>
    private IUsuariosEmpresa Usuarios => usuariosDe();

    public async Task<SesionEmpresa?> EjecutarAsync(
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
            return null;
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
            return null;
        }

        var verificacion = hash.Verificar(usuario.HashContrasena, peticion.Contrasena);

        if (!verificacion.EsValida)
        {
            log.LogInformation("Inicio de sesion rechazado para {Slug}/{Correo}.", slug, correo);
            return null;
        }

        // El login exitoso es el UNICO momento en que tenemos la contrasena en claro, y
        // por tanto el unico en que se puede regenerar un hash con costo viejo.
        var hashNuevo = verificacion.NecesitaRehash ? hash.Hash(peticion.Contrasena) : null;

        await Usuarios.RegistrarAccesoAsync(usuario.Id, DateTime.UtcNow, hashNuevo, ct);

        // ---------- LA COMPUERTA: permisos del rol interseccion modulos del plan ------
        var accesoTotal = await Usuarios.TieneAccesoTotalAsync(usuario.Id, ct);

        IReadOnlyList<string> permisos = [];

        if (!accesoTotal)
        {
            var delRol = await Usuarios.PermisosDeAsync(usuario.Id, ct);

            // Un permiso concedido sobre un modulo que el plan no incluye NO se otorga.
            // Si se dejara pasar, el permiso ganaria sobre lo contratado.
            permisos = delRol
                .Where(p => tenant.IncluyeModulo(ModuloDe(p)))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToList();

            log.LogDebug(
                "{Correo}: {Efectivos} permisos efectivos de {Totales} del rol.",
                correo, permisos.Count, delRol.Count);
        }

        // ---------- sesion de refresco ----------
        var refresco = tokens.Generar();

        await Usuarios.CrearSesionAsync(new SesionRefresh
        {
            UsuarioId = usuario.Id,
            HashToken = refresco.Hash,
            ExpiraEn = DateTime.UtcNow.AddDays(30),
            Ip = ip is null ? null : System.Net.IPAddress.TryParse(ip, out var dir) ? dir : null,
            AgenteUsuario = agente,
        }, ct);

        var emitido = proveedor.EmitirDeEmpresa(
            usuario.Id, usuario.Correo, usuario.Nombre, tenant.Id, tenant.Slug,
            accesoTotal, permisos);

        log.LogInformation("{Correo} inicio sesion en {Slug}.", correo, slug);

        return new SesionEmpresa(
            emitido.Token, emitido.ExpiraEn, refresco.EnClaro,
            usuario.Nombre, usuario.Correo, tenant.Slug, accesoTotal, permisos);
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
