using Maquinaria.Dominio.Organizacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class PuestoConfiguracion : IEntityTypeConfiguration<Puesto>
{
    public void Configure(EntityTypeBuilder<Puesto> puesto)
    {
        puesto.ToTable("puesto");

        puesto.HasKey(p => p.Id);

        puesto.Property(p => p.CreadoEn).HasDefaultValueSql("now()");

        puesto.HasIndex(p => p.Codigo)
            .IsUnique()
            .HasDatabaseName("puesto_codigo_unico");
    }
}
