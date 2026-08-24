using Maquinaria.Dominio.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class TipoEquipoConfiguracion : IEntityTypeConfiguration<TipoEquipo>
{
    public void Configure(EntityTypeBuilder<TipoEquipo> tipo)
    {
        tipo.ToTable("tipo_equipo");

        tipo.HasKey(t => t.Id);

        tipo.Property(t => t.CreadoEn).HasDefaultValueSql("now()");

        // UNICO POR CATEGORIA, no global: "Ligera" puede existir como tipo dentro de dos
        // categorias distintas sin que sea un error.
        tipo.HasIndex(t => new { t.CategoriaEquipoId, t.Codigo })
            .IsUnique()
            .HasDatabaseName("tipo_equipo_codigo_unico");

        // Restrict: una categoria con tipos colgando no se borra. Se marca inactiva.
        tipo.HasOne(t => t.Categoria)
            .WithMany(c => c.Tipos)
            .HasForeignKey(t => t.CategoriaEquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tipo_equipo_categoria");
    }
}
