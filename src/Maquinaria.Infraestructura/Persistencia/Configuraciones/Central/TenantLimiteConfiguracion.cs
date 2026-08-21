using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class TenantLimiteConfiguracion : IEntityTypeConfiguration<TenantLimite>
{
    public void Configure(EntityTypeBuilder<TenantLimite> limite)
    {
        limite.ToTable("tenant_limite", tabla =>
            tabla.HasCheckConstraint("tenant_limite_valor", "valor >= -1"));

        limite.HasKey(l => l.Id);

        // Una empresa no puede tener dos cupos del mismo tipo. El indice unico es
        // lo que vuelve segura la lectura "el limite del tenant o el por defecto":
        // sin el, dos filas contradictorias harian que el resultado dependiera del
        // orden de lectura.
        limite.HasIndex(l => new { l.TenantId, l.TipoLimiteId })
            .IsUnique()
            .HasDatabaseName("tenant_limite_unico");

        // Cascade: los cupos de una empresa no significan nada sin la empresa.
        limite.HasOne(l => l.Tenant)
            .WithMany(t => t.Limites)
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_tenant_limite_tenant");

        // Restrict, y WithMany() sin argumento: TipoLimite no expone coleccion de
        // cupos porque nadie navega de un tipo a todas las empresas que lo fijaron.
        // Un tipo de limite en uso se retira con activo = false, no se borra.
        limite.HasOne(l => l.TipoLimite)
            .WithMany()
            .HasForeignKey(l => l.TipoLimiteId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_tenant_limite_tipo");
    }
}
