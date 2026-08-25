using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ExtensionRentaConfiguracion : IEntityTypeConfiguration<ExtensionRenta>
{
    public void Configure(EntityTypeBuilder<ExtensionRenta> extension)
    {
        extension.ToTable("extension_renta", tabla =>
        {
            // Una prorroga que no alarga nada no es una prorroga.
            tabla.HasCheckConstraint("extension_avanza", "fin_nuevo > fin_anterior");
        });

        extension.HasKey(e => e.Id);

        extension.Property(e => e.CreadoEn).HasDefaultValueSql("now()");

        extension.HasOne(e => e.Renta)
            .WithMany(r => r.Extensiones)
            .HasForeignKey(e => e.RentaId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_extension_renta");

        extension.HasOne(e => e.Trabajador)
            .WithMany()
            .HasForeignKey(e => e.TrabajadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_extension_trabajador");
    }
}
