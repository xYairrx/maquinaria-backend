using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class UsuarioRolConfiguracion : IEntityTypeConfiguration<UsuarioRol>
{
    public void Configure(EntityTypeBuilder<UsuarioRol> usuarioRol)
    {
        usuarioRol.ToTable("usuario_rol");

        usuarioRol.HasKey(ur => new { ur.UsuarioId, ur.RolId });

        usuarioRol.HasOne(ur => ur.Usuario)
            .WithMany(u => u.Roles)
            .HasForeignKey(ur => ur.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_usuario_rol_usuario");

        // Restrict: un rol asignado a alguien no se borra sin quitarlo primero.
        usuarioRol.HasOne(ur => ur.Rol)
            .WithMany(r => r.Usuarios)
            .HasForeignKey(ur => ur.RolId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_usuario_rol_rol");
    }
}
