using Maquinaria.Dominio.Compras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class OrdenCompraDetalleConfiguracion
    : IEntityTypeConfiguration<OrdenCompraDetalle>
{
    public void Configure(EntityTypeBuilder<OrdenCompraDetalle> detalle)
    {
        detalle.ToTable("orden_compra_detalle", tabla =>
        {
            tabla.HasCheckConstraint("orden_compra_detalle_cantidad", "cantidad > 0");

            tabla.HasCheckConstraint(
                "orden_compra_detalle_montos", "costo_unitario >= 0 AND importe >= 0");
        });

        detalle.HasKey(d => d.Id);

        detalle.Property(d => d.CostoUnitario).HasColumnType("numeric(18,4)");
        detalle.Property(d => d.Importe).HasColumnType("numeric(18,4)");

        // DEFAULT en la base y no solo en C#: el diseno documentado los declara, y una
        // base que no los tiene hace que el documento mienta. Salio al probar la
        // migracion con SQL directo, que es como la va a tocar cualquiera que no pase
        // por EF.
        detalle.Property(d => d.Cantidad).HasDefaultValue(1);
        detalle.Property(d => d.Orden).HasDefaultValue(0);

        // Un equipo nace de UN renglon de compra. Dos renglones que reclamen la misma
        // maquina significaria haberla dado de alta dos veces.
        detalle.HasIndex(d => d.EquipoId)
            .IsUnique()
            .HasDatabaseName("orden_compra_detalle_equipo_unico")
            .HasFilter("equipo_id IS NOT NULL");

        detalle.HasIndex(d => d.OrdenCompraId)
            .HasDatabaseName("ix_orden_compra_detalle_orden");

        detalle.HasOne(d => d.OrdenCompra)
            .WithMany(o => o.Detalles)
            .HasForeignKey(d => d.OrdenCompraId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_orden_compra_detalle_orden");

        // Apunta al MODELO porque al comprar la maquina todavia no existe.
        detalle.HasOne(d => d.ModeloEquipo)
            .WithMany()
            .HasForeignKey(d => d.ModeloEquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_orden_compra_detalle_modelo");

        // Y aqui, nulo hasta que la orden se finaliza y nace el equipo.
        detalle.HasOne(d => d.Equipo)
            .WithMany()
            .HasForeignKey(d => d.EquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_orden_compra_detalle_equipo");
    }
}
