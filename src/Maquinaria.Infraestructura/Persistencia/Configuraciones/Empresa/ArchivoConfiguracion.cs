using Maquinaria.Dominio.Archivos;
using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ArchivoConfiguracion : IEntityTypeConfiguration<Archivo>
{
    public void Configure(EntityTypeBuilder<Archivo> archivo)
    {
        archivo.ToTable("archivo", tabla =>
            tabla.HasCheckConstraint("archivo_tamano", "tamano_bytes > 0"));

        archivo.HasKey(a => a.Id);

        archivo.Property(a => a.CreadoEn)
            .HasDefaultValueSql("now()");

        archivo.HasIndex(a => a.Ruta)
            .IsUnique()
            .HasDatabaseName("archivo_ruta_unica");

        // Indice PARCIAL sobre los vigentes: con baja logica, casi toda consulta
        // quiere solo los que siguen existiendo en el almacenamiento.
        archivo.HasIndex(a => a.CreadoEn)
            .IsDescending()
            .HasDatabaseName("ix_archivo_vigentes")
            .HasFilter("eliminado_en IS NULL");

        // Restrict y no Cascade: los usuarios no se borran, asi que esto no deberia
        // dispararse nunca. Y si algun dia se borrara uno, sus archivos no pueden
        // irse con el — los referencian evidencias y expedientes.
        archivo.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(a => a.SubidoPorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_archivo_subido_por");
    }
}
