using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class PermisoConfiguracion : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> permiso)
    {
        permiso.ToTable("permiso");

        permiso.HasKey(p => p.Id);

        permiso.HasIndex(p => p.Clave)
            .IsUnique()
            .HasDatabaseName("permiso_clave_unica");

        // Se consulta por modulo al resolver la interseccion con los modulos del
        // plan, y al sembrar los permisos de un modulo nuevo.
        permiso.HasIndex(p => p.Modulo)
            .HasDatabaseName("ix_permiso_modulo");
    }
}
