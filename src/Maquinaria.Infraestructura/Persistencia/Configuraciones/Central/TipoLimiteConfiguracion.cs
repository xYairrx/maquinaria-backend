using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class TipoLimiteConfiguracion : IEntityTypeConfiguration<TipoLimite>
{
    public void Configure(EntityTypeBuilder<TipoLimite> tipo)
    {
        tipo.ToTable("tipo_limite", tabla =>
            tabla.HasCheckConstraint("tipo_limite_defecto", "valor_defecto >= -1"));

        tipo.HasKey(t => t.Id);

        // valor_defecto SIN DEFAULT en la base. Con un DEFAULT -1, EF Core
        // omitiria la columna al insertar un tipo con ValorDefecto = 0 —0 es el
        // valor sentinel de int— y un limite que quiso decir "cero permitido" se
        // guardaria como ilimitado. El valor inicial lo pone el inicializador de
        // la entidad, donde si se puede distinguir.
        tipo.HasIndex(t => t.Clave)
            .IsUnique()
            .HasDatabaseName("tipo_limite_clave_unica");
    }
}
