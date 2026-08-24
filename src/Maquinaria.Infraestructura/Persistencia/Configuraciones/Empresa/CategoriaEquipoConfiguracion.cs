using Maquinaria.Dominio.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class CategoriaEquipoConfiguracion : IEntityTypeConfiguration<CategoriaEquipo>
{
    public void Configure(EntityTypeBuilder<CategoriaEquipo> categoria)
    {
        categoria.ToTable("categoria_equipo");

        categoria.HasKey(c => c.Id);

        categoria.Property(c => c.CreadoEn).HasDefaultValueSql("now()");

        categoria.HasIndex(c => c.Codigo)
            .IsUnique()
            .HasDatabaseName("categoria_equipo_codigo_unico");
    }
}
