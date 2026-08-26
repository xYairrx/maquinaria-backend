using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class TenantConfiguracion : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> tenant)
    {
        tenant.ToTable("tenant", tabla =>
        {
            // Control de seguridad, no cosmetica: el nombre de la base se
            // concatena en el CREATE DATABASE porque los identificadores SQL no
            // se pueden parametrizar.
            tabla.HasCheckConstraint(
                "tenant_bd_formato",
                "nombre_bd ~ '^[a-z][a-z0-9_]{2,62}$'");

            tabla.HasCheckConstraint(
                "tenant_slug_formato",
                "slug ~ '^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$'");

            tabla.HasCheckConstraint("tenant_moneda_valida", "length(moneda) = 3");

            tabla.HasCheckConstraint(
                "tenant_dia_pago",
                "dia_pago IS NULL OR dia_pago BETWEEN 1 AND 31");

            // Los enums arrancan en 1: esto rechaza el 0 que dejaria una
            // propiedad sin asignar.
            tabla.HasCheckConstraint("tenant_estado", "estado BETWEEN 1 AND 4");
            tabla.HasCheckConstraint(
                "tenant_aprovisionamiento",
                "estado_aprovisionamiento BETWEEN 1 AND 4");
        });

        tenant.HasKey(t => t.Id);

        tenant.Property(t => t.ZonaHoraria)
            .HasDefaultValue("America/Mexico_City");

        tenant.Property(t => t.Moneda)
            .HasDefaultValue("MXN");

        // Falso por defecto, y eso resuelve las filas que ya existen: quedan en «no se sabe»,
        // que es la verdad. Ponerlas en true daria por entregada una invitacion que nadie
        // puede confirmar y esconderia el boton de reenviar justo donde hace falta.
        tenant.Property(t => t.InvitacionEnviada)
            .HasDefaultValue(false);

        tenant.Property(t => t.CreadoEn)
            .HasDefaultValueSql("now()");

        tenant.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("tenant_slug_unico");

        tenant.HasIndex(t => t.NombreBd)
            .IsUnique()
            .HasDatabaseName("tenant_bd_unica");

        // Indice PARCIAL: con borrado logico, casi toda consulta quiere solo los
        // vivos, y asi el indice no carga con los borrados.
        tenant.HasIndex(t => t.Estado)
            .HasDatabaseName("ix_tenant_estado")
            .HasFilter("eliminado_en IS NULL");
    }
}
