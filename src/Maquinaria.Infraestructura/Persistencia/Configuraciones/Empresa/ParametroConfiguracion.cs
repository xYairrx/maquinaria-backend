using Maquinaria.Dominio.Configuracion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ParametroConfiguracion : IEntityTypeConfiguration<Parametro>
{
    public void Configure(EntityTypeBuilder<Parametro> parametro)
    {
        parametro.ToTable("parametro", tabla =>
            tabla.HasCheckConstraint("parametro_tipo", "tipo BETWEEN 1 AND 6"));

        parametro.HasKey(p => p.Id);

        parametro.HasIndex(p => p.Clave)
            .IsUnique()
            .HasDatabaseName("parametro_clave_unica");
    }
}
