using Maquinaria.Dominio.Catalogos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ModeloEquipoConfiguracion : IEntityTypeConfiguration<ModeloEquipo>
{
    public void Configure(EntityTypeBuilder<ModeloEquipo> modelo)
    {
        modelo.ToTable("modelo_equipo", tabla =>
            tabla.HasCheckConstraint(
                "modelo_horas_servicio", "horas_entre_servicios IS NULL OR horas_entre_servicios > 0"));

        modelo.HasKey(m => m.Id);

        modelo.Property(m => m.CreadoEn).HasDefaultValueSql("now()");

        modelo.HasIndex(m => new { m.MarcaId, m.Nombre })
            .IsUnique()
            .HasDatabaseName("modelo_equipo_unico");

        modelo.HasOne(m => m.Marca)
            .WithMany(ma => ma.Modelos)
            .HasForeignKey(m => m.MarcaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_modelo_equipo_marca");

        modelo.HasOne(m => m.TipoEquipo)
            .WithMany()
            .HasForeignKey(m => m.TipoEquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_modelo_equipo_tipo");
    }
}
