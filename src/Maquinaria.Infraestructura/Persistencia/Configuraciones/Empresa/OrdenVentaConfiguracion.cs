using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class OrdenVentaConfiguracion : IEntityTypeConfiguration<OrdenVenta>
{
    public void Configure(EntityTypeBuilder<OrdenVenta> orden)
    {
        orden.ToTable("orden_venta", tabla =>
        {
            tabla.HasCheckConstraint("orden_venta_estado", "estado BETWEEN 1 AND 4");

            tabla.HasCheckConstraint(
                "orden_venta_montos",
                "subtotal >= 0 AND descuento >= 0 AND impuestos >= 0 AND total >= 0");

            // La equivalencia es en los DOS sentidos: finalizada exige fecha, y una fecha
            // exige estar finalizada. Sin esto el reporte de ventas del mes miente.
            tabla.HasCheckConstraint(
                "orden_venta_finalizacion", "(estado = 3) = (finalizada_en IS NOT NULL)");
        });

        orden.HasKey(o => o.Id);

        orden.Property(o => o.Fecha).HasDefaultValueSql("current_date");

        orden.Property(o => o.CreadoEn).HasDefaultValueSql("now()");

        orden.Property(o => o.Subtotal).HasColumnType("numeric(18,4)");
        orden.Property(o => o.Descuento).HasColumnType("numeric(18,4)");
        orden.Property(o => o.Impuestos).HasColumnType("numeric(18,4)");
        orden.Property(o => o.Total).HasColumnType("numeric(18,4)");

        orden.HasIndex(o => o.Folio)
            .IsUnique()
            .HasDatabaseName("orden_venta_folio_unico");

        orden.HasIndex(o => o.Estado)
            .HasDatabaseName("ix_orden_venta_estado");

        orden.HasOne(o => o.Cliente)
            .WithMany()
            .HasForeignKey(o => o.ClienteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_orden_venta_cliente");

        orden.HasOne(o => o.Trabajador)
            .WithMany()
            .HasForeignKey(o => o.TrabajadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_orden_venta_trabajador");
    }
}
