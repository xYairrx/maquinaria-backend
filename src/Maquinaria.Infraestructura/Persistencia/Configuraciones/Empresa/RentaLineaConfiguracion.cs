using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class RentaLineaConfiguracion : IEntityTypeConfiguration<RentaLinea>
{
    public void Configure(EntityTypeBuilder<RentaLinea> linea)
    {
        linea.ToTable("renta_linea", tabla =>
        {
            tabla.HasCheckConstraint("renta_linea_cantidad", "cantidad > 0");

            tabla.HasCheckConstraint(
                "renta_linea_montos", "precio_unitario >= 0 AND importe >= 0");
        });

        linea.HasKey(l => l.Id);

        linea.Property(l => l.Cantidad).HasColumnType("numeric(12,2)");
        linea.Property(l => l.PrecioUnitario).HasColumnType("numeric(18,4)");
        linea.Property(l => l.HorasIncluidas).HasColumnType("numeric(12,2)");
        linea.Property(l => l.Importe).HasColumnType("numeric(18,4)");
        linea.Property(l => l.HorometroSalida).HasColumnType("numeric(12,2)");
        linea.Property(l => l.HorometroDevolucion).HasColumnType("numeric(12,2)");

        // DEFAULT en la base y no solo en C#: el diseno documentado los declara, y una
        // base que no los tiene hace que el documento mienta. Salio al probar la
        // migracion con SQL directo, que es como la va a tocar cualquiera que no pase
        // por EF.
        linea.Property(l => l.Orden).HasDefaultValue(0);

        // La misma maquina con la misma tarifa no se repite en una renta. Dos renglones
        // iguales serian un cobro doble por descuido.
        linea.HasIndex(l => new { l.RentaId, l.EquipoId, l.TarifaId })
            .IsUnique()
            .HasDatabaseName("renta_linea_unica");

        linea.HasIndex(l => l.RentaId)
            .HasDatabaseName("ix_renta_linea_renta");

        linea.HasIndex(l => l.EquipoId)
            .HasDatabaseName("ix_renta_linea_equipo");

        linea.HasOne(l => l.Renta)
            .WithMany(r => r.Lineas)
            .HasForeignKey(l => l.RentaId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_renta_linea_renta");

        linea.HasOne(l => l.Equipo)
            .WithMany()
            .HasForeignKey(l => l.EquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_renta_linea_equipo");

        linea.HasOne(l => l.Tarifa)
            .WithMany()
            .HasForeignKey(l => l.TarifaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_renta_linea_tarifa");
    }
}
