using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class PlanModuloConfiguracion : IEntityTypeConfiguration<PlanModulo>
{
    public void Configure(EntityTypeBuilder<PlanModulo> planModulo)
    {
        planModulo.ToTable("plan_modulo");

        // Llave compuesta, sin uuid de sustitucion: nadie referencia una fila de
        // esta tabla. Mismo criterio que rol_permiso y usuario_rol.
        planModulo.HasKey(pm => new { pm.PlanId, pm.ModuloId });

        // Cascade: quitar un plan se lleva su composicion, que no significa nada
        // sin el. La FK de suscripcion a plan es RESTRICT, asi que un plan con
        // historial no se puede borrar de todas formas.
        planModulo.HasOne(pm => pm.Plan)
            .WithMany(p => p.Modulos)
            .HasForeignKey(pm => pm.PlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_plan_modulo_plan");

        // Restrict: un modulo en uso por algun plan no se borra. Se retira con
        // activo = false.
        planModulo.HasOne(pm => pm.Modulo)
            .WithMany(m => m.Planes)
            .HasForeignKey(pm => pm.ModuloId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_plan_modulo_modulo");
    }
}
