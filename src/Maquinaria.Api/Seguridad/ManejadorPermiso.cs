using Maquinaria.Infraestructura.Seguridad;
using Microsoft.AspNetCore.Authorization;

namespace Maquinaria.Api.Seguridad;

/// <summary>
/// Decide si el token de la peticion trae el permiso que el endpoint exige.
///
/// LOS PERMISOS SALEN DEL TOKEN, no de la base. Es justo lo que se compro metiendolos
/// dentro al iniciar sesion: cada peticion se autoriza sin una consulta, y el precio es que
/// revocar un permiso tarda en surtir efecto lo que dure el token —15 minutos— porque el
/// refresco rotativo vuelve a resolver la compuerta.
/// </summary>
internal sealed class ManejadorPermiso : AuthorizationHandler<RequisitoPermiso>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto, RequisitoPermiso requisito)
    {
        // acceso_total SALTA la verificacion, no la sustituye. Lo emite
        // ProveedorTokensJwt para el administrador de la empresa, y cuando esta presente el
        // claim de permisos NO viene: la compuerta ya decidio que este usuario los tiene
        // todos, asi que enumerarlos seria una lista que se desincroniza.
        if (contexto.User.HasClaim(ProveedorTokensJwt.ClaimAccesoTotal, "true"))
        {
            contexto.Succeed(requisito);
            return Task.CompletedTask;
        }

        var concedidos = contexto.User.FindFirst(ProveedorTokensJwt.ClaimPermisos)?.Value;

        if (concedidos is not null && Contiene(concedidos, requisito.Clave))
        {
            contexto.Succeed(requisito);
        }

        // SIN Fail() a proposito: no conceder ya niega —si ningun manejador tuvo exito, la
        // autorizacion falla—. Fail() en cambio es definitivo y cerraria la puerta a que
        // otro manejador del mismo requisito concediera, que es lo que haria falta el dia
        // que haya permisos por ubicacion o por cliente.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Busca la clave dentro del claim, que es una lista separada por espacios.
    ///
    /// SE RECORRE EN LUGAR DE PARTIR LA CADENA: <c>Split</c> asignaria un arreglo y una
    /// cadena por permiso en CADA peticion autorizada, y un usuario con acceso amplio
    /// trae mas de cien. <see cref="MemoryExtensions.Split"/> sobre el span no asigna nada.
    ///
    /// La comparacion es ordinal y exacta, no por prefijo: si fuera por prefijo,
    /// <c>equipos.editar</c> concederia <c>equipos.editar-todo</c> el dia que exista un
    /// permiso asi.
    /// </summary>
    private static bool Contiene(string concedidos, string clave)
    {
        foreach (var rango in concedidos.AsSpan().Split(' '))
        {
            if (concedidos.AsSpan(rango).SequenceEqual(clave))
            {
                return true;
            }
        }

        return false;
    }
}
