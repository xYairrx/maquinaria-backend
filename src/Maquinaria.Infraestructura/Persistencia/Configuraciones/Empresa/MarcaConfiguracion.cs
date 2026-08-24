using Maquinaria.Dominio.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class MarcaConfiguracion : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> marca)
    {
        marca.ToTable("marca");

        marca.HasKey(m => m.Id);

        marca.Property(m => m.CreadoEn).HasDefaultValueSql("now()");

        marca.HasIndex(m => m.Nombre)
            .IsUnique()
            .HasDatabaseName("marca_nombre_unico");
    }
}
