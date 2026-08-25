using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class RentaConfiguracion : IEntityTypeConfiguration<Renta>
{
    public void Configure(EntityTypeBuilder<Renta> renta)
    {
        renta.ToTable("renta", tabla =>
        {
            tabla.HasCheckConstraint("renta_estado", "estado BETWEEN 1 AND 10");

            tabla.HasCheckConstraint("renta_periodo", "fin > inicio");

            tabla.HasCheckConstraint(
                "renta_montos",
                "deposito >= 0 AND anticipo >= 0 AND subtotal >= 0 "
                + "AND descuento >= 0 AND impuestos >= 0 AND total >= 0");

            // btrim y no solo NOT NULL: una cadena de espacios pasa el NOT NULL y deja
            // una renta sin lugar, que es una maquina que nadie sabe donde esta.
            tabla.HasCheckConstraint(
                "renta_lugar_no_vacio", "length(btrim(lugar_descripcion)) > 0");

            tabla.HasCheckConstraint(
                "renta_lugar_coordenadas",
                "(lugar_latitud IS NULL) = (lugar_longitud IS NULL)");
        });

        renta.HasKey(r => r.Id);

        renta.Property(r => r.CreadoEn).HasDefaultValueSql("now()");

        renta.Property(r => r.LugarLatitud).HasColumnType("numeric(9,6)");
        renta.Property(r => r.LugarLongitud).HasColumnType("numeric(9,6)");

        renta.Property(r => r.Deposito).HasColumnType("numeric(18,4)");
        renta.Property(r => r.Anticipo).HasColumnType("numeric(18,4)");
        renta.Property(r => r.Subtotal).HasColumnType("numeric(18,4)");
        renta.Property(r => r.Descuento).HasColumnType("numeric(18,4)");
        renta.Property(r => r.Impuestos).HasColumnType("numeric(18,4)");
        renta.Property(r => r.Total).HasColumnType("numeric(18,4)");
        renta.Property(r => r.Saldo).HasColumnType("numeric(18,4)");

        renta.HasIndex(r => r.Folio)
            .IsUnique()
            .HasDatabaseName("renta_folio_unico");

        renta.HasIndex(r => new { r.ClienteId, r.Inicio })
            .HasDatabaseName("ix_renta_cliente")
            .IsDescending(false, true);

        renta.HasIndex(r => r.Estado)
            .HasDatabaseName("ix_renta_estado");

        renta.HasOne(r => r.Cliente)
            .WithMany()
            .HasForeignKey(r => r.ClienteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_renta_cliente");

        // Nulable: no toda renta viene de una cotizacion. Muchas de repeticion se
        // levantan directo, y exigirla obligaria a inventar cotizaciones falsas.
        renta.HasOne(r => r.Cotizacion)
            .WithMany()
            .HasForeignKey(r => r.CotizacionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_renta_cotizacion");

        renta.HasOne(r => r.Trabajador)
            .WithMany()
            .HasForeignKey(r => r.TrabajadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_renta_trabajador");
    }
}
