using Maquinaria.Dominio.Activos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class EquipoTarifaConfiguracion : IEntityTypeConfiguration<EquipoTarifa>
{
    public void Configure(EntityTypeBuilder<EquipoTarifa> precio)
    {
        precio.ToTable("equipo_tarifa", tabla =>
        {
            tabla.HasCheckConstraint("equipo_tarifa_precio", "precio >= 0");

            // Codigo ISO de tres letras. Sin esto entran "peso", "MXN " y "$".
            tabla.HasCheckConstraint("equipo_tarifa_moneda", "length(moneda) = 3");

            tabla.HasCheckConstraint(
                "equipo_tarifa_vigencia",
                "vigencia_hasta IS NULL OR vigencia_hasta > vigencia_desde");
        });

        precio.HasKey(p => p.Id);

        precio.Property(p => p.CreadoEn).HasDefaultValueSql("now()");

        precio.Property(p => p.Precio).HasColumnType("numeric(18,4)");

        precio.Property(p => p.Moneda).HasDefaultValue("MXN");

        precio.HasOne(p => p.Equipo)
            .WithMany(e => e.Tarifas)
            .HasForeignKey(p => p.EquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_equipo_tarifa_equipo");

        precio.HasOne(p => p.Tarifa)
            .WithMany()
            .HasForeignKey(p => p.TarifaId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_equipo_tarifa_tarifa");

        precio.HasOne(p => p.Cliente)
            .WithMany()
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_equipo_tarifa_cliente");

        // La restriccion que impide dos precios vigentes a la vez es un EXCLUDE USING
        // gist, y va en SQL crudo en la migracion: EF Core no sabe expresarla.
    }
}
