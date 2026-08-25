using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class OrdenVentaDetalleConfiguracion
    : IEntityTypeConfiguration<OrdenVentaDetalle>
{
    public void Configure(EntityTypeBuilder<OrdenVentaDetalle> detalle)
    {
        detalle.ToTable("orden_venta_detalle", tabla =>
        {
            tabla.HasCheckConstraint(
                "orden_venta_detalle_montos", "precio_unitario >= 0 AND importe >= 0");
        });

        detalle.HasKey(d => d.Id);

        detalle.Property(d => d.PrecioUnitario).HasColumnType("numeric(18,4)");
        detalle.Property(d => d.Importe).HasColumnType("numeric(18,4)");

        // DEFAULT en la base y no solo en C#: el diseno documentado los declara, y una
        // base que no los tiene hace que el documento mienta. Salio al probar la
        // migracion con SQL directo, que es como la va a tocar cualquiera que no pase
        // por EF.
        detalle.Property(d => d.Orden).HasDefaultValue(0);

        // La misma maquina no puede aparecer dos veces en la misma orden: no hay dos
        // unidades de una maquina concreta.
        detalle.HasIndex(d => new { d.OrdenVentaId, d.EquipoId })
            .IsUnique()
            .HasDatabaseName("orden_venta_detalle_unico");

        detalle.HasIndex(d => d.OrdenVentaId)
            .HasDatabaseName("ix_orden_venta_detalle_orden");

        detalle.HasOne(d => d.OrdenVenta)
            .WithMany(o => o.Detalles)
            .HasForeignKey(d => d.OrdenVentaId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_orden_venta_detalle_orden");

        detalle.HasOne(d => d.Equipo)
            .WithMany()
            .HasForeignKey(d => d.EquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_orden_venta_detalle_equipo");
    }
}
