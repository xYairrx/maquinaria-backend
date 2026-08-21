using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Dominio.Plataforma;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Plataforma;

public sealed class UsuariosPlataformaEf(ContextoCentral contexto) : IUsuariosPlataforma
{
    public Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken ct)
        => contexto.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo, ct);

    public async Task RegistrarAccesoAsync(
        Guid usuarioId, DateTime cuandoUtc, string? hashNuevo, CancellationToken ct)
    {
        // ExecuteUpdateAsync y no cargar-modificar-guardar: es una escritura de una
        // columna que no necesita el grafo en memoria ni disparar el interceptor de
        // auditoria. El ultimo acceso no es un cambio auditable de negocio.
        if (hashNuevo is null)
        {
            await contexto.Usuarios
                .Where(u => u.Id == usuarioId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.UltimoAccesoEn, cuandoUtc), ct);

            return;
        }

        await contexto.Usuarios
            .Where(u => u.Id == usuarioId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.UltimoAccesoEn, cuandoUtc)
                      .SetProperty(u => u.HashContrasena, hashNuevo),
                ct);
    }

    public Task<bool> ExisteAlgunoAsync(CancellationToken ct)
        => contexto.Usuarios.AnyAsync(ct);

    public async Task CrearAsync(Usuario usuario, CancellationToken ct)
    {
        contexto.Usuarios.Add(usuario);
        await contexto.SaveChangesAsync(ct);
    }
}
