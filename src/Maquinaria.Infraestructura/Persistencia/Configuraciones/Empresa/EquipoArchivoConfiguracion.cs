using Maquinaria.Dominio.Activos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class EquipoArchivoConfiguracion : IEntityTypeConfiguration<EquipoArchivo>
{
    public void Configure(EntityTypeBuilder<EquipoArchivo> enlace)
    {
        enlace.ToTable("equipo_archivo", tabla =>
        {
            tabla.HasCheckConstraint("equipo_archivo_tipo", "tipo BETWEEN 1 AND 6");
        });

        enlace.HasKey(e => e.Id);

        enlace.Property(e => e.CreadoEn).HasDefaultValueSql("now()");

        // El mismo archivo no se adjunta dos veces a la misma maquina.
        enlace.HasIndex(e => new { e.EquipoId, e.ArchivoId })
            .IsUnique()
            .HasDatabaseName("equipo_archivo_unico");

        enlace.HasOne(e => e.Equipo)
            .WithMany(e => e.Archivos)
            .HasForeignKey(e => e.EquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_equipo_archivo_equipo");

        enlace.HasOne(e => e.Archivo)
            .WithMany()
            .HasForeignKey(e => e.ArchivoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_equipo_archivo_archivo");
    }
}
