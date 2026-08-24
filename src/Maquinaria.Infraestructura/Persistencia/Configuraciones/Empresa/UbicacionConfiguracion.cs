using Maquinaria.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class UbicacionConfiguracion : IEntityTypeConfiguration<Ubicacion>
{
    public void Configure(EntityTypeBuilder<Ubicacion> ubicacion)
    {
        ubicacion.ToTable("ubicacion", tabla =>
        {
            tabla.HasCheckConstraint("ubicacion_tipo", "tipo BETWEEN 1 AND 4");

            // Las dos o ninguna: media coordenada no ubica nada, y dejarla a medias
            // haria que el calculo de rutas de la Fase 2 tuviera que desconfiar del dato.
            tabla.HasCheckConstraint(
                "ubicacion_coordenadas",
                "(latitud IS NULL) = (longitud IS NULL)");

            tabla.HasCheckConstraint(
                "ubicacion_latitud", "latitud IS NULL OR latitud BETWEEN -90 AND 90");

            tabla.HasCheckConstraint(
                "ubicacion_longitud", "longitud IS NULL OR longitud BETWEEN -180 AND 180");
        });

        ubicacion.HasKey(u => u.Id);

        ubicacion.Property(u => u.CreadoEn).HasDefaultValueSql("now()");

        // numeric y no double: son coordenadas geograficas, no calculos de punto
        // flotante, y 6 decimales dan precision de ~10 cm.
        ubicacion.Property(u => u.Latitud).HasColumnType("numeric(9,6)");
        ubicacion.Property(u => u.Longitud).HasColumnType("numeric(9,6)");

        ubicacion.HasIndex(u => new { u.SucursalId, u.Codigo })
            .IsUnique()
            .HasDatabaseName("ubicacion_codigo_unico");

        ubicacion.HasOne(u => u.Sucursal)
            .WithMany(s => s.Ubicaciones)
            .HasForeignKey(u => u.SucursalId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_ubicacion_sucursal");
    }
}
