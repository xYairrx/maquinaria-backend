using Microsoft.AspNetCore.Authorization;

namespace Maquinaria.Api.Seguridad;

/// <summary>
/// Exige un permiso concreto —<c>equipos.crear</c>— sobre el token de la peticion.
///
/// Es un requisito y no un <c>RequireClaim</c> porque los permisos viajan en UN SOLO claim
/// separado por espacios, y <c>RequireClaim</c> compara el valor completo del claim, no una
/// palabra dentro de el. Quien decide es <see cref="ManejadorPermiso"/>.
/// </summary>
internal sealed class RequisitoPermiso(string clave) : IAuthorizationRequirement
{
    public string Clave { get; } = clave;
}
