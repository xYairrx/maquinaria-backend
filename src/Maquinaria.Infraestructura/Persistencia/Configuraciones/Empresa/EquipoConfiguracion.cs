using Maquinaria.Dominio.Activos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class EquipoConfiguracion : IEntityTypeConfiguration<Equipo>
{
    public void Configure(EntityTypeBuilder<Equipo> equipo)
    {
        equipo.ToTable("equipo", tabla =>
        {
            tabla.HasCheckConstraint("equipo_estado", "estado BETWEEN 1 AND 8");

            tabla.HasCheckConstraint("equipo_proposito", "proposito BETWEEN 1 AND 3");

            tabla.HasCheckConstraint("equipo_origen", "origen BETWEEN 1 AND 2");

            tabla.HasCheckConstraint(
                "equipo_anio", "anio IS NULL OR anio BETWEEN 1900 AND 2200");

            // COALESCE y no "> 0": el nulo es legitimo —no siempre se conoce el costo—,
            // pero un negativo nunca lo es.
            tabla.HasCheckConstraint(
                "equipo_montos",
                "COALESCE(costo_adquisicion, 0) >= 0 AND COALESCE(valor_actual, 0) >= 0");

            tabla.HasCheckConstraint(
                "equipo_lecturas",
                "COALESCE(horometro, 0) >= 0 AND COALESCE(kilometraje, 0) >= 0");
        });

        equipo.HasKey(e => e.Id);

        equipo.Property(e => e.CreadoEn).HasDefaultValueSql("now()");

        equipo.Property(e => e.CostoAdquisicion).HasColumnType("numeric(18,4)");
        equipo.Property(e => e.ValorActual).HasColumnType("numeric(18,4)");
        equipo.Property(e => e.Horometro).HasColumnType("numeric(12,2)");
        equipo.Property(e => e.Kilometraje).HasColumnType("numeric(12,2)");

        equipo.HasIndex(e => e.CodigoInterno)
            .IsUnique()
            .HasDatabaseName("equipo_codigo_unico");

        // El token del QR es unico: dos maquinas con la misma etiqueta harian que
        // escanear dejara de identificar nada.
        equipo.HasIndex(e => e.TokenQr)
            .IsUnique()
            .HasDatabaseName("equipo_token_qr_unico")
            .HasFilter("token_qr IS NOT NULL");

        // PARCIALES sobre eliminado_en IS NULL: las pantallas de disponibilidad y de
        // inventario nunca preguntan por las maquinas dadas de baja, y cargarlas en el
        // indice solo lo engorda.
        equipo.HasIndex(e => e.Estado)
            .HasDatabaseName("ix_equipo_estado")
            .HasFilter("eliminado_en IS NULL");

        equipo.HasIndex(e => e.UbicacionId)
            .HasDatabaseName("ix_equipo_ubicacion")
            .HasFilter("eliminado_en IS NULL");

        equipo.HasIndex(e => e.ModeloEquipoId)
            .HasDatabaseName("ix_equipo_modelo");

        // La serie se busca por fragmentos: la gente teclea los ultimos cuatro digitos.
        equipo.HasIndex(e => e.NumeroSerie)
            .HasDatabaseName("ix_equipo_serie")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");

        equipo.HasOne(e => e.Modelo)
            .WithMany()
            .HasForeignKey(e => e.ModeloEquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_equipo_modelo");

        equipo.HasOne(e => e.Tipo)
            .WithMany()
            .HasForeignKey(e => e.TipoEquipoId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_equipo_tipo");

        // Restrict y no Cascade: borrar una bodega NO puede llevarse las maquinas por
        // delante. Si alguien intenta borrar una ubicacion con equipo, tiene que moverlo
        // antes.
        equipo.HasOne(e => e.Ubicacion)
            .WithMany()
            .HasForeignKey(e => e.UbicacionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_equipo_ubicacion");
    }
}
