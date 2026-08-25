using Maquinaria.Dominio.Compras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class OrdenCompraConfiguracion : IEntityTypeConfiguration<OrdenCompra>
{
    public void Configure(EntityTypeBuilder<OrdenCompra> orden)
    {
        orden.ToTable("orden_compra", tabla =>
        {
            tabla.HasCheckConstraint("orden_compra_estado", "estado BETWEEN 1 AND 4");

            tabla.HasCheckConstraint(
                "orden_compra_montos", "subtotal >= 0 AND impuestos >= 0 AND total >= 0");

            tabla.HasCheckConstraint(
                "orden_compra_finalizacion", "(estado = 3) = (finalizada_en IS NOT NULL)");
        });

        orden.HasKey(o => o.Id);

        orden.Property(o => o.Fecha).HasDefaultValueSql("current_date");

        orden.Property(o => o.CreadoEn).HasDefaultValueSql("now()");

        orden.Property(o => o.Subtotal).HasColumnType("numeric(18,4)");
        orden.Property(o => o.Impuestos).HasColumnType("numeric(18,4)");
        orden.Property(o => o.Total).HasColumnType("numeric(18,4)");

        orden.HasIndex(o => o.Folio)
            .IsUnique()
            .HasDatabaseName("orden_compra_folio_unico");

        orden.HasIndex(o => o.Estado)
            .HasDatabaseName("ix_orden_compra_estado");

        // La orden es la UNICA duena del proveedor. Cuando quitaste proveedor_id de
        // equipo, de quien se compro paso a ser un hecho de aqui y de ningun otro lugar.
        orden.HasOne(o => o.Proveedor)
            .WithMany()
            .HasForeignKey(o => o.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_orden_compra_proveedor");

        orden.HasOne(o => o.Trabajador)
            .WithMany()
            .HasForeignKey(o => o.TrabajadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_orden_compra_trabajador");
    }
}
