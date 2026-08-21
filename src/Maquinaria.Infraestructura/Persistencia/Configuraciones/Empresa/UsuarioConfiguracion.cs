using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> usuario)
    {
        // Singular y explicito: sin este ToTable, UseSnakeCaseNamingConvention
        // tomaria el nombre del DbSet —Usuarios— y crearia la tabla "usuarios".
        usuario.ToTable("usuario", tabla =>
            tabla.HasCheckConstraint("usuario_estado", "estado BETWEEN 1 AND 4"));

        usuario.HasKey(u => u.Id);

        usuario.Property(u => u.CreadoEn)
            .HasDefaultValueSql("now()");

        // UNIQUE GLOBAL, no parcial por estado. Los usuarios no se borran, asi que
        // un correo nunca se libera. Es deliberado: un unico parcial volveria
        // ambiguo el login, que tendria que filtrar por estado antes de validar.
        usuario.HasIndex(u => u.Correo)
            .IsUnique()
            .HasDatabaseName("usuario_correo_unico");

        usuario.HasIndex(u => u.Estado)
            .HasDatabaseName("ix_usuario_estado");
    }
}
