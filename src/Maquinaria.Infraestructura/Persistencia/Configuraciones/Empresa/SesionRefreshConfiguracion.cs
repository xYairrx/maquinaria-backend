using Maquinaria.Dominio.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

internal sealed class SesionRefreshConfiguracion : IEntityTypeConfiguration<SesionRefresh>
{
    public void Configure(EntityTypeBuilder<SesionRefresh> sesion)
    {
        sesion.ToTable("sesion_refresh", tabla =>
            tabla.HasCheckConstraint("sesion_refresh_vigencia", "expira_en > creado_en"));

        sesion.HasKey(s => s.Id);

        sesion.Property(s => s.CreadoEn)
            .HasDefaultValueSql("now()");

        sesion.HasIndex(s => s.HashToken)
            .IsUnique()
            .HasDatabaseName("sesion_refresh_hash_unico");

        sesion.HasIndex(s => s.UsuarioId)
            .HasDatabaseName("ix_sesion_usuario_activa")
            .HasFilter("revocado_en IS NULL");

        sesion.HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_sesion_refresh_usuario");

        // Autorreferencia: forma la cadena de rotacion. Restrict, no Cascade:
        // borrar un eslabon no debe llevarse la cadena, que es justo la evidencia
        // de un reuso.
        sesion.HasOne(s => s.ReemplazadoPor)
            .WithMany()
            .HasForeignKey(s => s.ReemplazadoPorId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_sesion_refresh_reemplazo");
    }
}
