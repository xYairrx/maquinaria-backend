using Maquinaria.Dominio.Plataforma;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Central;

internal sealed class ModuloConfiguracion : IEntityTypeConfiguration<Modulo>
{
    public void Configure(EntityTypeBuilder<Modulo> modulo)
    {
        modulo.ToTable("modulo", tabla =>
            tabla.HasCheckConstraint("modulo_numero_rango", "numero BETWEEN 1 AND 99"));

        modulo.HasKey(m => m.Id);

        // Sin creado_en: es un catalogo de codigo, sembrado por migracion, igual
        // que permiso en la base de empresa. Nadie lo da de alta a mano, asi que
        // la marca de tiempo no responderia ninguna pregunta.
        //
        // Y sin DEFAULT en activo, a proposito: un DEFAULT true haria que EF Core
        // omitiera la columna al insertar un modulo con Activo = false —false es
        // el valor sentinel de bool— y se guardaria activo.
        modulo.HasIndex(m => m.Clave)
            .IsUnique()
            .HasDatabaseName("modulo_clave_unica");

        modulo.HasIndex(m => m.Numero)
            .IsUnique()
            .HasDatabaseName("modulo_numero_unico");
    }
}
