using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class RolPermisoConfiguracion : IEntityTypeConfiguration<RolPermiso>
{
    public void Configure(EntityTypeBuilder<RolPermiso> rolPermiso)
    {
        rolPermiso.ToTable("rol_permiso");

        // Llave compuesta, sin uuid de sustitucion: nadie referencia una fila de
        // esta tabla.
        rolPermiso.HasKey(rp => new { rp.RolId, rp.PermisoId });

        rolPermiso.HasOne(rp => rp.Rol)
            .WithMany(r => r.Permisos)
            .HasForeignKey(rp => rp.RolId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_rol_permiso_rol");

        // Restrict: un permiso concedido a algun rol no se borra. Los permisos son
        // catalogo de codigo; si un modulo desaparece, su retiro es una migracion
        // que primero quita las concesiones.
        rolPermiso.HasOne(rp => rp.Permiso)
            .WithMany(p => p.Roles)
            .HasForeignKey(rp => rp.PermisoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_rol_permiso_permiso");
    }
}
