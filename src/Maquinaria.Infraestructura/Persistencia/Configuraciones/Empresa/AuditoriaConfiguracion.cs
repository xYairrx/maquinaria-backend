using Maquinaria.Dominio.Trazabilidad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Maquinaria.Infraestructura.Persistencia.Configuraciones.Empresa;

/// <summary>
/// La misma entidad <see cref="Auditoria"/> se configura en los dos contextos, con
/// el mismo esquema. Esta es la de la base de cada empresa, donde se registra la operacion del negocio.
///
/// El cuerpo es identico al de la configuracion del otro contexto a proposito: la
/// tabla no tiene ni una relacion, asi que no hay nada que particularizar. Si algun
/// dia divergen, es un error, no una decision.
/// </summary>
internal sealed class AuditoriaConfiguracion : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> auditoria)
    {
        auditoria.ToTable("auditoria", tabla =>
            tabla.HasCheckConstraint("auditoria_accion", "accion BETWEEN 1 AND 8"));

        auditoria.HasKey(a => a.Id);

        // GENERATED ALWAYS, no BY DEFAULT: la aplicacion NO PUEDE suministrar un id.
        // En una bitacora append-only eso importa, porque nadie puede insertar en
        // una posicion arbitraria de la secuencia ni pisar un numero.
        auditoria.Property(a => a.Id)
            .UseIdentityAlwaysColumn();

        // El reloj es el de la base, no el del servidor de aplicacion: con varias
        // instancias sus relojes derivan y el orden del registro dejaria de ser
        // confiable justo cuando se necesita.
        auditoria.Property(a => a.FechaUtc)
            .HasDefaultValueSql("now()");

        // string[] -> text[] nativo en Npgsql. Sin conversor y sin dependencia en
        // el dominio.
        auditoria.Property(a => a.Roles)
            .HasColumnType("text[]");

        // jsonb y no json: viene parseado e indexable, soporta ? y @>, y esta tabla
        // existe para ser interrogada.
        auditoria.Property(a => a.ValoresAnteriores)
            .HasColumnType("jsonb");

        auditoria.Property(a => a.ValoresNuevos)
            .HasColumnType("jsonb");

        // SIN NINGUNA FK, incluida usuario_id. No es solo el costo de verificacion
        // en la tabla mas escrita: usuario_id puede apuntar legitimamente a una fila
        // que no existe en esta base, asi que una FK seria incorrecta.
        auditoria.HasIndex(a => a.FechaUtc)
            .IsDescending()
            .HasDatabaseName("ix_auditoria_fecha");

        auditoria.HasIndex(a => new { a.Entidad, a.EntidadId })
            .HasDatabaseName("ix_auditoria_entidad");

        // "Que hizo esta persona": el filtro mas comun de una revision.
        auditoria.HasIndex(a => new { a.UsuarioId, a.FechaUtc })
            .IsDescending(false, true)
            .HasDatabaseName("ix_auditoria_usuario");

        // "Que se hizo en esta operacion": reconstruye la accion completa.
        auditoria.HasIndex(a => a.CorrelacionId)
            .HasDatabaseName("ix_auditoria_correlacion");
    }
}
