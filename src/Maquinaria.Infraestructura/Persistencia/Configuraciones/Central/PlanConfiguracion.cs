using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class PlanConfiguracion : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> plan)
    {
        plan.ToTable("plan", t =>
        {
            t.HasCheckConstraint("plan_precio_valido", "precio_mensual >= 0");
            t.HasCheckConstraint("plan_moneda_valida", "length(moneda) = 3");
        });

        plan.HasKey(p => p.Id);

        plan.Property(p => p.PrecioMensual)
            .HasColumnType("numeric(18,4)");

        plan.Property(p => p.Moneda)
            .HasDefaultValue("MXN");

        plan.Property(p => p.CreadoEn)
            .HasDefaultValueSql("now()");

        plan.HasIndex(p => p.Codigo)
            .IsUnique()
            .HasDatabaseName("plan_codigo_unico");
    }
}