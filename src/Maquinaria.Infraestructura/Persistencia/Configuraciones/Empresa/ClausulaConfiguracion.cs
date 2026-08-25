using Maquinaria.Dominio.Comercial;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ClausulaConfiguracion : IEntityTypeConfiguration<Clausula>
{
    public void Configure(EntityTypeBuilder<Clausula> clausula)
    {
        clausula.ToTable("clausula", tabla =>
            tabla.HasCheckConstraint("clausula_texto_no_vacio", "length(btrim(texto)) > 0"));

        clausula.HasKey(c => c.Id);

        clausula.Property(c => c.CreadoEn).HasDefaultValueSql("now()");

        clausula.HasIndex(c => c.Codigo)
            .IsUnique()
            .HasDatabaseName("clausula_codigo_unico");

        // Las obligatorias se consultan cada vez que se arma un contrato, y son pocas
        // frente al total. Indice parcial: no carga con las opcionales.
        clausula.HasIndex(c => c.Orden)
            .HasDatabaseName("ix_clausula_obligatorias")
            .HasFilter("obligatoria AND activo");
    }
}
