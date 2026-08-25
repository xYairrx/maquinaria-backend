using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class TransferenciaEquipoConfiguracion
    : IEntityTypeConfiguration<TransferenciaEquipo>
{
    public void Configure(EntityTypeBuilder<TransferenciaEquipo> traspaso)
    {
        traspaso.ToTable("transferencia_equipo", tabla =>
        {
            // Un traspaso de una bodega a si misma no es un traspaso.
            tabla.HasCheckConstraint("transferencia_distinta", "origen_id <> destino_id");
        });

        traspaso.HasKey(t => t.Id);

        traspaso.Property(t => t.Fecha).HasDefaultValueSql("now()");

        traspaso.Property(t => t.CreadoEn).HasDefaultValueSql("now()");

        // DESCENDENTE por fecha: la consulta real es "los ultimos movimientos de esta
        // maquina", nunca los primeros.
        traspaso.HasIndex(t => new { t.EquipoId, t.Fecha })
            .HasDatabaseName("ix_transferencia_equipo")
            .IsDescending(false, true);

        traspaso.HasOne(t => t.Equipo)
            .WithMany()
            .HasForeignKey(t => t.EquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transferencia_equipo");

        traspaso.HasOne(t => t.Origen)
            .WithMany()
            .HasForeignKey(t => t.OrigenId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transferencia_origen");

        traspaso.HasOne(t => t.Destino)
            .WithMany()
            .HasForeignKey(t => t.DestinoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transferencia_destino");

        traspaso.HasOne(t => t.Trabajador)
            .WithMany()
            .HasForeignKey(t => t.TrabajadorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transferencia_trabajador");

        // Que origen y destino resguarden equipo lo exige un disparador, no un CHECK:
        // la regla depende del tipo de OTRA fila y un CHECK no puede consultarla.
    }
}
