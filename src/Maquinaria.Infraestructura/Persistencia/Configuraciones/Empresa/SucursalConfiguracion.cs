using Maquinaria.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class SucursalConfiguracion : IEntityTypeConfiguration<Sucursal>
{
    public void Configure(EntityTypeBuilder<Sucursal> sucursal)
    {
        sucursal.ToTable("sucursal");

        sucursal.HasKey(s => s.Id);

        sucursal.Property(s => s.CreadoEn).HasDefaultValueSql("now()");

        sucursal.HasIndex(s => s.Codigo)
            .IsUnique()
            .HasDatabaseName("sucursal_codigo_unico");
    }
}
