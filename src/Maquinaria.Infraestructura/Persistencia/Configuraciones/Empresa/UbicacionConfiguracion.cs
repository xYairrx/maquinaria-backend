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
            tabla.HasCheckConstraint("ubicacion_tipo", "tipo BETWEEN 1 AND 3");

            // Las dos o ninguna: media coordenada no ubica nada, y dejarla a medias
            // haria que el calculo de rutas de la Fase 2 tuviera que desconfiar del dato.
            tabla.HasCheckConstraint(
                "ubicacion_coordenadas", "(latitud IS NULL) = (longitud IS NULL)");

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

        // El codigo es unico GLOBAL, no por sucursal: ya no hay sucursal padre.
        ubicacion.HasIndex(u => u.Codigo)
            .IsUnique()
            .HasDatabaseName("ubicacion_codigo_unico");

        ubicacion.HasIndex(u => u.Tipo)
            .HasDatabaseName("ix_ubicacion_tipo");

        // EF las ignora porque no las escribe la aplicacion: en la base son COLUMNAS
        // GENERADAS a partir de tipo, y se agregan con SQL crudo en la migracion junto
        // con los indices que permiten referenciarlas. EF Core no sabe expresar
        // GENERATED ALWAYS ... STORED, y tampoco intenta borrar columnas que no conoce.
        ubicacion.Ignore(u => u.AlmacenaEquipo);
        ubicacion.Ignore(u => u.EsAdministrativa);
    }
}
