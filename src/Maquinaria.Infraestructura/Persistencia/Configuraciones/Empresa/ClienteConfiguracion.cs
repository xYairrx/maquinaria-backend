using Maquinaria.Dominio.Terceros;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> cliente)
    {
        cliente.ToTable("cliente", tabla =>
        {
            tabla.HasCheckConstraint("cliente_estado", "estado BETWEEN 1 AND 3");

            tabla.HasCheckConstraint(
                "cliente_credito", "limite_credito >= 0 AND dias_credito >= 0");

            tabla.HasCheckConstraint("cliente_deposito", "deposito_requerido >= 0");

            // Las dos o ninguna, igual que en ubicacion: media coordenada no ubica nada.
            tabla.HasCheckConstraint(
                "cliente_coordenadas", "(latitud IS NULL) = (longitud IS NULL)");
        });

        cliente.HasKey(c => c.Id);

        cliente.Property(c => c.CreadoEn).HasDefaultValueSql("now()");

        cliente.Property(c => c.Pais).HasDefaultValue("MX");

        cliente.Property(c => c.Latitud).HasColumnType("numeric(9,6)");
        cliente.Property(c => c.Longitud).HasColumnType("numeric(9,6)");

        cliente.Property(c => c.LimiteCredito).HasColumnType("numeric(18,4)");
        cliente.Property(c => c.DepositoRequerido).HasColumnType("numeric(18,4)");

        cliente.HasIndex(c => c.Codigo)
            .IsUnique()
            .HasDatabaseName("cliente_codigo_unico");

        cliente.HasIndex(c => c.Estado)
            .HasDatabaseName("ix_cliente_estado");

        // GIN de trigramas, igual que proveedor: a un cliente se le busca por un trozo
        // del nombre —"constru"— y un btree no acelera eso.
        cliente.HasIndex(c => c.RazonSocial)
            .HasDatabaseName("ix_cliente_razon_social")
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops");
    }
}
