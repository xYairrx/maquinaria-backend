using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class CotizacionLineaConfiguracion
    : IEntityTypeConfiguration<CotizacionLinea>
{
    public void Configure(EntityTypeBuilder<CotizacionLinea> linea)
    {
        linea.ToTable("cotizacion_linea", tabla =>
        {
            tabla.HasCheckConstraint("cotizacion_linea_cantidad", "cantidad > 0");

            tabla.HasCheckConstraint(
                "cotizacion_linea_montos", "precio_unitario >= 0 AND importe >= 0");

            // AQUI NO VA un CHECK que exija equipo o tipo_equipo. La primera version lo
            // tenia y hacia IMPOSIBLE cotizar un flete, que no es ni una maquina ni un
            // tipo de maquina. Se quito al descubrirlo.
        });

        linea.HasKey(l => l.Id);

        linea.Property(l => l.Cantidad).HasColumnType("numeric(12,2)");
        linea.Property(l => l.PrecioUnitario).HasColumnType("numeric(18,4)");
        linea.Property(l => l.Importe).HasColumnType("numeric(18,4)");

        // DEFAULT en la base y no solo en C#: el diseno documentado los declara, y una
        // base que no los tiene hace que el documento mienta. Salio al probar la
        // migracion con SQL directo, que es como la va a tocar cualquiera que no pase
        // por EF.
        linea.Property(l => l.Orden).HasDefaultValue(0);

        linea.HasIndex(l => l.CotizacionId)
            .HasDatabaseName("ix_cotizacion_linea_cotizacion");

        // CASCADE, y es el unico lugar donde se permite: una linea sin cotizacion no
        // significa nada, asi que muere con ella. Las FK a catalogos son Restrict.
        linea.HasOne(l => l.Cotizacion)
            .WithMany(c => c.Lineas)
            .HasForeignKey(l => l.CotizacionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_cotizacion_linea_cotizacion");

        linea.HasOne(l => l.Tarifa)
            .WithMany()
            .HasForeignKey(l => l.TarifaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cotizacion_linea_tarifa");

        linea.HasOne(l => l.Equipo)
            .WithMany()
            .HasForeignKey(l => l.EquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cotizacion_linea_equipo");

        linea.HasOne(l => l.TipoEquipo)
            .WithMany()
            .HasForeignKey(l => l.TipoEquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cotizacion_linea_tipo");
    }
}
