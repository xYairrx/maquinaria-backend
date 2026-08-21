using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class PlanLimiteConfiguracion : IEntityTypeConfiguration<PlanLimite>
{
    public void Configure(EntityTypeBuilder<PlanLimite> limite)
    {
        limite.ToTable("plan_limite", t =>
            t.HasCheckConstraint("plan_limite_valor", "valor >= -1"));

        limite.HasKey(l => l.Id);

        limite.HasIndex(l => new { l.PlanId, l.Clave })
            .IsUnique()
            .HasDatabaseName("plan_limite_unico");

        limite.HasOne(l => l.Plan)
            .WithMany(p => p.Limites)
            .HasForeignKey(l => l.PlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_plan_limite_plan");
    }
}
