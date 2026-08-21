using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class UsuarioPlataformaConfiguracion : IEntityTypeConfiguration<UsuarioPlataforma>
{
    public void Configure(EntityTypeBuilder<UsuarioPlataforma> usuario)
    {
        usuario.ToTable("usuario_plataforma");

        usuario.HasKey(u => u.Id);

        usuario.Property(u => u.CreadoEn)
            .HasDefaultValueSql("now()");

        usuario.HasIndex(u => u.Correo)
            .IsUnique()
            .HasDatabaseName("usuario_plataforma_correo_unico");
    }
}