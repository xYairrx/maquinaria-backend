using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class SuscripcionConfiguracion : IEntityTypeConfiguration<Suscripcion>
{
    public void Configure(EntityTypeBuilder<Suscripcion> suscripcion)
    {
        suscripcion.ToTable("suscripcion", tabla =>
        {
            tabla.HasCheckConstraint(
                "suscripcion_periodo_valido",
                "fin IS NULL OR fin > inicio");

            tabla.HasCheckConstraint("suscripcion_estado", "estado BETWEEN 1 AND 4");

            // OJO: el constraint EXCLUDE de no-traslape NO se puede declarar aqui.
            // EF Core no sabe expresarlo. Va como SQL crudo en la migracion.
        });

        suscripcion.HasKey(s => s.Id);

        suscripcion.Property(s => s.CreadoEn)
            .HasDefaultValueSql("now()");

        suscripcion.HasIndex(s => s.TenantId)
            .HasDatabaseName("ix_suscripcion_tenant");

        suscripcion.HasOne(s => s.Tenant)
            .WithMany(t => t.Suscripciones)
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_suscripcion_tenant");

        // WithMany() sin argumento: Plan no expone coleccion de suscripciones.
        // Restrict, no Cascade: un plan retirado se marca inactivo y nunca se
        // borra, precisamente porque estas filas historicas lo referencian.
        suscripcion.HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_suscripcion_plan");
    }
}
