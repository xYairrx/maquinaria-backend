using System.Text;
using Maquinaria.Aplicacion.Plataforma;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Maquinaria.Infraestructura.Seguridad;

public sealed class ProveedorTokensJwt : IProveedorTokens
{
    /// <summary>Marca el tipo de sujeto, para no depender solo de la audiencia.</summary>
    public const string ClaimAmbito = "ambito";

    public const string AmbitoPlataforma = "plataforma";

    /// <summary>Ambito de los usuarios de una empresa. Lo usara la rebanada de auth de empresa.</summary>
    public const string AmbitoEmpresa = "empresa";

    /// <summary>Id del tenant. Lo lee MiddlewareTenant para resolver la base.</summary>
    public const string ClaimTenant = "tenant";

    /// <summary>Slug, para que la interfaz pueda mostrarlo. No es secreto.</summary>
    public const string ClaimEmpresa = "empresa";

    /// <summary>Presente y en "true" solo cuando el rol salta la verificacion.</summary>
    public const string ClaimAccesoTotal = "acceso_total";

    /// <summary>
    /// Permisos efectivos, separados por espacios en UN claim.
    ///
    /// Un claim por permiso multiplicaria por cien la cabecera de cada peticion; una
    /// sola cadena separada por espacios es como lo hace OAuth con los scopes.
    /// </summary>
    public const string ClaimPermisos = "perm";

    /// <summary>
    /// Los CODIGOS de los roles, separados por espacios, con el mismo formato que
    /// <see cref="ClaimPermisos"/>.
    ///
    /// No autoriza nada —eso lo siguen haciendo los permisos y acceso_total— y existe
    /// solo para la auditoria: es lo que permite preguntarle a la bitacora si una accion
    /// paso por el bypass de acceso_total o por un permiso concedido. Van aqui y no se
    /// consultan por peticion porque los roles que importan son los que autorizaron ESTA
    /// peticion, y esos son los del momento de emitir el token, igual que los permisos.
    /// </summary>
    public const string ClaimRoles = "roles";

    private readonly OpcionesJwt _opciones;
    private readonly SigningCredentials _credenciales;

    public ProveedorTokensJwt(IOptions<OpcionesJwt> opciones)
    {
        _opciones = opciones.Value;

        if (Encoding.UTF8.GetByteCount(_opciones.Llave) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Llave debe tener al menos 32 bytes para HMAC-SHA256. Va en secretos, "
                + "nunca en appsettings.json.");
        }

        var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opciones.Llave));
        _credenciales = new SigningCredentials(llave, SecurityAlgorithms.HmacSha256);
    }

    public TokenEmitido EmitirDePlataforma(Guid usuarioId, string correo, string nombre)
    {
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(_opciones.MinutosPlataforma);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Emisor,
            Audience = _opciones.AudienciaPlataforma,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            SigningCredentials = _credenciales,
            Claims = new Dictionary<string, object>
            {
                // Nombres CORTOS de JWT, no los ClaimTypes de .NET: ClaimTypes.Email
                // se serializa como el URI completo de WS-Federation
                // (http://schemas.xmlsoap.org/...), que son ~55 bytes de relleno por
                // claim en cada peticion, para nada.
                [JwtRegisteredClaimNames.Sub] = usuarioId.ToString(),
                [JwtRegisteredClaimNames.Email] = correo,
                [JwtRegisteredClaimNames.Name] = nombre,
                [ClaimAmbito] = AmbitoPlataforma,

                // NO va nada mas. En particular, ningun nombre_bd ni identificador de
                // tenant: un JWT va firmado pero NO cifrado, asi que cualquiera que lo
                // tenga puede leer su contenido. Los nombres de las bases de datos de
                // los clientes no viajan al navegador.
            },
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new TokenEmitido(token, expira);
    }

    public TokenEmitido EmitirDeEmpresa(
        Guid usuarioId,
        string correo,
        string nombre,
        Guid tenantId,
        string slug,
        bool accesoTotal,
        IReadOnlyList<string> permisos,
        IReadOnlyList<string> roles)
    {
        var ahora = DateTime.UtcNow;
        var expira = ahora.AddMinutes(_opciones.MinutosEmpresa);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = usuarioId.ToString(),
            [JwtRegisteredClaimNames.Email] = correo,
            [JwtRegisteredClaimNames.Name] = nombre,
            [ClaimAmbito] = AmbitoEmpresa,

            // El ID del tenant, NUNCA el nombre_bd. Un JWT va firmado pero no cifrado.
            [ClaimTenant] = tenantId.ToString(),
            [ClaimEmpresa] = slug,
        };

        // Aunque haya acceso_total. Ahi es donde MAS importa saber cual de los roles
        // trajo el bypass: es la unica forma de responderlo despues, porque los roles y
        // rol_permiso cambian.
        if (roles.Count > 0)
        {
            claims[ClaimRoles] = string.Join(' ', roles);
        }

        if (accesoTotal)
        {
            // Un claim en lugar de 156. Y ademas es lo honesto: el rol no tiene esos
            // permisos concedidos, salta la verificacion, que no es lo mismo.
            claims[ClaimAccesoTotal] = "true";
        }
        else if (permisos.Count > 0)
        {
            claims[ClaimPermisos] = string.Join(' ', permisos);
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _opciones.Emisor,
            Audience = _opciones.AudienciaEmpresa,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expira,
            SigningCredentials = _credenciales,
            Claims = claims,
        };

        return new TokenEmitido(new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}
