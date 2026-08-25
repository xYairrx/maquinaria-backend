using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class TarifaConfiguracion : IEntityTypeConfiguration<Tarifa>
{
    public void Configure(EntityTypeBuilder<Tarifa> tarifa)
    {
        tarifa.ToTable("tarifa", tabla =>
        {
            tabla.HasCheckConstraint("tarifa_unidad", "unidad BETWEEN 1 AND 6");

            // Una tarifa que no aplica en ningun lado no se puede usar para nada, y
            // seria una fila muerta que igual aparece en los catalogos de la interfaz.
            tabla.HasCheckConstraint(
                "tarifa_aplica_en_algo", "aplica_renta OR aplica_venta");
        });

        tarifa.HasKey(t => t.Id);

        tarifa.Property(t => t.CreadoEn).HasDefaultValueSql("now()");

        tarifa.HasIndex(t => t.Codigo)
            .IsUnique()
            .HasDatabaseName("tarifa_codigo_unico");
    }
}
