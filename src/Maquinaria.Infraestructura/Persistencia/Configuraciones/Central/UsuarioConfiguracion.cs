using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> usuario)
    {
        // Singular y explicito. Sin este ToTable, UseSnakeCaseNamingConvention
        // tomaria el nombre del DbSet —Usuarios— y crearia la tabla "usuarios".
        usuario.ToTable("usuario");

        usuario.HasKey(u => u.Id);

        usuario.Property(u => u.CreadoEn)
            .HasDefaultValueSql("now()");

        usuario.HasIndex(u => u.Correo)
            .IsUnique()
            .HasDatabaseName("usuario_correo_unico");
    }
}
