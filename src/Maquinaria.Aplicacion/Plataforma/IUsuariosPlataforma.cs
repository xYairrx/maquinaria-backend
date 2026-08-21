using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// Acceso a los superadministradores de la base central. La implementacion con EF
/// Core vive en Infraestructura: Aplicacion no conoce ningun DbContext.
/// </summary>
public interface IUsuariosPlataforma
{
    /// <summary>El correo llega ya normalizado a minusculas por el caso de uso.</summary>
    Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken ct);

    Task RegistrarAccesoAsync(Guid usuarioId, DateTime cuandoUtc, string? hashNuevo, CancellationToken ct);

    /// <summary>
    /// Para el arranque: decide si hay que crear el primer superadministrador.
    /// </summary>
    Task<bool> ExisteAlgunoAsync(CancellationToken ct);

    Task CrearAsync(Usuario usuario, CancellationToken ct);
}
