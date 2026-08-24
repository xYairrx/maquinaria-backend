using Maquinaria.Dominio.Terceros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ProveedorConfiguracion : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> proveedor)
    {
        proveedor.ToTable("proveedor");

        proveedor.HasKey(p => p.Id);

        proveedor.Property(p => p.CreadoEn).HasDefaultValueSql("now()");

        proveedor.HasIndex(p => p.Codigo)
            .IsUnique()
            .HasDatabaseName("proveedor_codigo_unico");

        // GIN con gin_trgm_ops, NO un btree.
        //
        // Un btree sobre texto solo sirve para igualdad y para prefijos: no acelera
        // "%excavadora%", que es como se busca un proveedor en la practica. El indice
        // de trigramas si, y pg_trgm ya se instala en toda base de empresa desde su
        // primera migracion.
        proveedor.HasIndex(p => p.RazonSocial)
            .HasDatabaseName("ix_proveedor_razon_social")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
