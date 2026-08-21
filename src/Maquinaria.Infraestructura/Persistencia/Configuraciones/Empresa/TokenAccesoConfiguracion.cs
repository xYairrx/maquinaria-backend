using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class TokenAccesoConfiguracion : IEntityTypeConfiguration<TokenAcceso>
{
    public void Configure(EntityTypeBuilder<TokenAcceso> token)
    {
        token.ToTable("token_acceso", tabla =>
        {
            tabla.HasCheckConstraint("token_acceso_vigencia", "expira_en > creado_en");
            tabla.HasCheckConstraint("token_acceso_proposito", "proposito BETWEEN 1 AND 2");
        });

        token.HasKey(t => t.Id);

        token.Property(t => t.CreadoEn)
            .HasDefaultValueSql("now()");

        token.HasIndex(t => t.HashToken)
            .IsUnique()
            .HasDatabaseName("token_acceso_hash_unico");

        // Indice PARCIAL: la consulta real es "tiene este usuario una liga viva",
        // y con borrado logico casi toda consulta quiere solo las vigentes.
        token.HasIndex(t => t.UsuarioId)
            .HasDatabaseName("ix_token_acceso_pendiente")
            .HasFilter("usado_en IS NULL AND invalidado_en IS NULL");

        token.HasOne(t => t.Usuario)
            .WithMany()
            .HasForeignKey(t => t.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_token_acceso_usuario");

        // CreadoPorId NO lleva navegacion ni FK obligatoria: puede ser NULL porque
        // la invitacion la crea un superadministrador que vive en la base central y
        // no existe aqui. Se declara la FK a usuario para el caso en que si sea de
        // esta base.
        token.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(t => t.CreadoPorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_token_acceso_creado_por");
    }
}
