using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class RolConfiguracion : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> rol)
    {
        rol.ToTable("rol");

        rol.HasKey(r => r.Id);

        rol.Property(r => r.CreadoEn)
            .HasDefaultValueSql("now()");

        rol.HasIndex(r => r.Codigo)
            .IsUnique()
            .HasDatabaseName("rol_codigo_unico");

        // UNICO PARCIAL sobre la propia columna, filtrado a las filas en true.
        // Como todas esas filas valen lo mismo, el unico admite COMO MAXIMO UNA:
        // es lo que impide crear un segundo rol con acceso total y escalar por ahi.
        //
        // El trigger que vuelve inmutable esa fila va como SQL crudo en la
        // migracion: EF Core no sabe expresar triggers.
        rol.HasIndex(r => r.AccesoTotal)
            .IsUnique()
            .HasDatabaseName("rol_acceso_total_unico")
            .HasFilter("acceso_total");
    }
}
