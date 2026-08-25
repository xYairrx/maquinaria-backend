using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ContratoClausulaConfiguracion
    : IEntityTypeConfiguration<ContratoClausula>
{
    public void Configure(EntityTypeBuilder<ContratoClausula> clausula)
    {
        clausula.ToTable("contrato_clausula", tabla =>
        {
            // Una clausula sin texto no obliga a nada.
            tabla.HasCheckConstraint("contrato_clausula_texto", "length(btrim(texto)) > 0");
        });

        clausula.HasKey(c => c.Id);

        clausula.Property(c => c.CreadoEn).HasDefaultValueSql("now()");

        // El orden importa en un contrato y no puede haber dos clausulas terceras.
        clausula.HasIndex(c => new { c.ContratoId, c.Orden })
            .IsUnique()
            .HasDatabaseName("contrato_clausula_orden_unico");

        clausula.HasOne(c => c.Contrato)
            .WithMany(c => c.Clausulas)
            .HasForeignKey(c => c.ContratoId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_contrato_clausula_contrato");

        // Nulable: la clausula puede ser propia, negociada con el cliente y sin origen
        // en el catalogo. Y aunque venga del catalogo, el TEXTO se copia: editar el
        // catalogo no puede cambiar un contrato ya firmado.
        clausula.HasOne(c => c.Clausula)
            .WithMany()
            .HasForeignKey(c => c.ClausulaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_contrato_clausula_clausula");
    }
}
