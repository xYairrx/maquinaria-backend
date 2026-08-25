using Maquinaria.Dominio.Activos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class OcupacionEquipoConfiguracion
    : IEntityTypeConfiguration<OcupacionEquipo>
{
    public void Configure(EntityTypeBuilder<OcupacionEquipo> ocupacion)
    {
        ocupacion.ToTable("ocupacion_equipo", tabla =>
        {
            tabla.HasCheckConstraint("ocupacion_motivo", "motivo BETWEEN 1 AND 6");

            // Un periodo que termina antes de empezar romperia el rango que usa la
            // restriccion de traslape.
            tabla.HasCheckConstraint("ocupacion_periodo", "fin IS NULL OR fin > inicio");
        });

        ocupacion.HasKey(o => o.Id);

        ocupacion.Property(o => o.CreadoEn).HasDefaultValueSql("now()");

        ocupacion.Property(o => o.Activo).HasDefaultValue(true);

        // Parcial sobre activo: las canceladas no compiten por el calendario y no tienen
        // por que ocupar el indice que resuelve "esta libre del 3 al 9?".
        ocupacion.HasIndex(o => new { o.EquipoId, o.Inicio })
            .HasDatabaseName("ix_ocupacion_equipo")
            .HasFilter("activo");

        ocupacion.HasOne(o => o.Equipo)
            .WithMany()
            .HasForeignKey(o => o.EquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ocupacion_equipo");

        // referencia_id NO lleva FK a proposito: apunta a una renta, a una orden de
        // trabajo o a nada segun el motivo, y una FK no puede cambiar de destino.

        // La garantia de no rentar dos veces las mismas fechas es un EXCLUDE USING gist
        // y va en SQL crudo en la migracion.
    }
}
