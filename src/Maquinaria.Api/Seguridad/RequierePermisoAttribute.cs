using Microsoft.AspNetCore.Authorization;

namespace Maquinaria.Api.Seguridad;

/// <summary>
/// Exige un permiso de la matriz sobre una accion o un controlador completo:
/// <c>[RequierePermiso("equipos.crear")]</c>.
///
/// Es un <see cref="AuthorizeAttribute"/> y no un filtro propio, y eso compra dos cosas que
/// un filtro tendria que reimplementar: la tuberia de autorizacion distingue sola el **401**
/// —no hay token— del **403** —hay token y no alcanza—, y el atributo se COMBINA con la
/// policy de ambito del controlador en lugar de correr por fuera de ella.
///
/// La policy que nombra la registra el bucle de Program.cs, una por cada clave de
/// <c>ClavesPermiso.Todas</c>. Consecuencia deliberada: **una clave mal escrita revienta**
/// al llegar la primera peticion, con «The AuthorizationPolicy named ... was not found». La
/// alternativa —un IAuthorizationPolicyProvider que fabrique la policy al vuelo— aceptaria
/// cualquier cadena y devolveria 403 para siempre, en silencio, sobre un endpoint que nadie
/// puede alcanzar. Eso tiene que doler al primer intento.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
internal sealed class RequierePermisoAttribute : AuthorizeAttribute
{
    public RequierePermisoAttribute(string clave) => Policy = clave;

    /// <summary>La clave exigida. La leen las pruebas por reflexion.</summary>
    public string Clave => Policy!;
}
