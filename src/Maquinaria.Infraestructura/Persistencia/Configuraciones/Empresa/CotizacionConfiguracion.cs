using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class CotizacionConfiguracion : IEntityTypeConfiguration<Cotizacion>
{
    public void Configure(EntityTypeBuilder<Cotizacion> cotizacion)
    {
        cotizacion.ToTable("cotizacion", tabla =>
        {
            tabla.HasCheckConstraint("cotizacion_estado", "estado BETWEEN 1 AND 7");

            tabla.HasCheckConstraint(
                "cotizacion_montos",
                "subtotal >= 0 AND descuento >= 0 AND impuestos >= 0 AND total >= 0");
        });

        cotizacion.HasKey(c => c.Id);

        cotizacion.Property(c => c.Fecha).HasDefaultValueSql("current_date");

        cotizacion.Property(c => c.CreadoEn).HasDefaultValueSql("now()");

        cotizacion.Property(c => c.Subtotal).HasColumnType("numeric(18,4)");
        cotizacion.Property(c => c.Descuento).HasColumnType("numeric(18,4)");
        cotizacion.Property(c => c.Impuestos).HasColumnType("numeric(18,4)");
        cotizacion.Property(c => c.Total).HasColumnType("numeric(18,4)");

        cotizacion.HasIndex(c => c.Folio)
            .IsUnique()
            .HasDatabaseName("cotizacion_folio_unico");

        // Descendente: se consulta "las ultimas cotizaciones de este cliente".
        cotizacion.HasIndex(c => new { c.ClienteId, c.Fecha })
            .HasDatabaseName("ix_cotizacion_cliente")
            .IsDescending(false, true);

        cotizacion.HasIndex(c => c.Estado)
            .HasDatabaseName("ix_cotizacion_estado");

        cotizacion.HasOne(c => c.Cliente)
            .WithMany()
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cotizacion_cliente");

        cotizacion.HasOne(c => c.Ubicacion)
            .WithMany()
            .HasForeignKey(c => c.UbicacionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cotizacion_ubicacion");

        cotizacion.HasOne(c => c.Trabajador)
            .WithMany()
            .HasForeignKey(c => c.TrabajadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_cotizacion_trabajador");

        // Que la ubicacion sea administrativa —sucursal o patio, nunca bodega— lo exige
        // un disparador contra la columna generada es_administrativa.
    }
}
