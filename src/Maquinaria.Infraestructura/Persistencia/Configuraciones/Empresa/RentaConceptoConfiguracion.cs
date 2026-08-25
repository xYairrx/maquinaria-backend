using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class RentaConceptoConfiguracion : IEntityTypeConfiguration<RentaConcepto>
{
    public void Configure(EntityTypeBuilder<RentaConcepto> concepto)
    {
        concepto.ToTable("renta_concepto", tabla =>
        {
            tabla.HasCheckConstraint("renta_concepto_cantidad", "cantidad > 0");

            tabla.HasCheckConstraint(
                "renta_concepto_montos",
                "precio_unitario >= 0 AND importe >= 0 AND COALESCE(costo, 0) >= 0");
        });

        concepto.HasKey(c => c.Id);

        concepto.Property(c => c.CreadoEn).HasDefaultValueSql("now()");

        concepto.Property(c => c.Cantidad).HasColumnType("numeric(12,2)").HasDefaultValue(1m);
        concepto.Property(c => c.PrecioUnitario).HasColumnType("numeric(18,4)");
        concepto.Property(c => c.Costo).HasColumnType("numeric(18,4)");
        concepto.Property(c => c.Importe).HasColumnType("numeric(18,4)");

        concepto.HasIndex(c => c.RentaId)
            .HasDatabaseName("ix_renta_concepto_renta");

        concepto.HasOne(c => c.Renta)
            .WithMany(r => r.Conceptos)
            .HasForeignKey(c => c.RentaId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_renta_concepto_renta");

        concepto.HasOne(c => c.Tarifa)
            .WithMany()
            .HasForeignKey(c => c.TarifaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_renta_concepto_tarifa");

        // El operador, cuando el concepto es un operador. Nulo para un flete.
        concepto.HasOne(c => c.Trabajador)
            .WithMany()
            .HasForeignKey(c => c.TrabajadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_renta_concepto_trabajador");
    }
}
