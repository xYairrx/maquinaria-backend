using Maquinaria.Dominio.Organizacion;
using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class TrabajadorConfiguracion : IEntityTypeConfiguration<Trabajador>
{
    public void Configure(EntityTypeBuilder<Trabajador> trabajador)
    {
        trabajador.ToTable("trabajador", tabla =>
        {
            tabla.HasCheckConstraint("trabajador_estado", "estado BETWEEN 1 AND 3");

            // Un trabajador de baja tiene fecha de baja, y uno que no lo esta no la
            // tiene. Sin esto, los dos estados incoherentes son indistinguibles de los
            // buenos, y el dia que alguien filtre por fecha_baja los datos mienten.
            tabla.HasCheckConstraint(
                "trabajador_baja_coherente",
                "(estado = 3) = (fecha_baja IS NOT NULL)");

            tabla.HasCheckConstraint(
                "trabajador_fechas",
                "fecha_baja IS NULL OR fecha_ingreso IS NULL OR fecha_baja >= fecha_ingreso");
        });

        trabajador.HasKey(t => t.Id);

        trabajador.Property(t => t.CreadoEn).HasDefaultValueSql("now()");

        trabajador.HasIndex(t => t.NumeroEmpleado)
            .IsUnique()
            .HasDatabaseName("trabajador_numero_unico");

        trabajador.HasIndex(t => t.Estado)
            .HasDatabaseName("ix_trabajador_estado");

        // UNICO PARCIAL: una cuenta pertenece como maximo a un trabajador, pero muchos
        // trabajadores no tienen cuenta. Sin el filtro, el unico contaria los NULL como
        // colision en algunos motores; en Postgres no, pero el indice parcial ademas no
        // carga con las filas que no interesan.
        trabajador.HasIndex(t => t.UsuarioId)
            .IsUnique()
            .HasDatabaseName("trabajador_usuario_unico")
            .HasFilter("usuario_id IS NOT NULL");

        trabajador.HasOne(t => t.Puesto)
            .WithMany(p => p.Trabajadores)
            .HasForeignKey(t => t.PuestoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_trabajador_puesto");

        trabajador.HasOne(t => t.Ubicacion)
            .WithMany()
            .HasForeignKey(t => t.UbicacionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_trabajador_ubicacion");

        // La FK va de este lado para NO tocar la tabla usuario, que es de la Fase 0 y ya
        // esta migrada en las bases que existan.
        trabajador.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_trabajador_usuario");
    }
}
