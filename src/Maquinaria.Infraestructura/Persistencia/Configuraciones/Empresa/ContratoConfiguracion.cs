using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ContratoConfiguracion : IEntityTypeConfiguration<Contrato>
{
    public void Configure(EntityTypeBuilder<Contrato> contrato)
    {
        contrato.ToTable("contrato", tabla =>
        {
            tabla.HasCheckConstraint("contrato_estado", "estado BETWEEN 1 AND 4");

            tabla.HasCheckConstraint("contrato_deposito", "deposito >= 0");

            tabla.HasCheckConstraint(
                "contrato_fechas", "fecha_fin IS NULL OR fecha_fin >= fecha_inicio");
        });

        contrato.HasKey(c => c.Id);

        contrato.Property(c => c.CreadoEn).HasDefaultValueSql("now()");

        contrato.Property(c => c.Deposito).HasColumnType("numeric(18,4)");

        contrato.HasIndex(c => c.Folio)
            .IsUnique()
            .HasDatabaseName("contrato_folio_unico");

        // UN CONTRATO POR RENTA. Si algun dia hace falta un contrato marco que cubra
        // varias rentas, hay que quitar esto. Se deja porque quitarlo despues es trivial
        // y ponerlo despues ya no se puede si hay datos que lo violan.
        contrato.HasIndex(c => c.RentaId)
            .IsUnique()
            .HasDatabaseName("contrato_renta_unica");

        contrato.HasIndex(c => new { c.ClienteId, c.FechaInicio })
            .HasDatabaseName("ix_contrato_cliente")
            .IsDescending(false, true);

        // Restrict y NO Cascade: borrar una renta no puede llevarse su contrato. Un
        // contrato firmado es un documento legal y sobrevive a cualquier limpieza.
        contrato.HasOne(c => c.Renta)
            .WithMany()
            .HasForeignKey(c => c.RentaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_contrato_renta");

        contrato.HasOne(c => c.Cliente)
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_contrato_cliente");

        // La inmutabilidad al salir de borrador la impone un disparador, en SQL crudo.
    }
}
