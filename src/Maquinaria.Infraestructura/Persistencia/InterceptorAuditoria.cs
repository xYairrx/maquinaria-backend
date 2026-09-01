using System.Text.Json;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Trazabilidad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Maquinaria.Infraestructura.Persistencia;

/// <summary>
/// Escribe la bitacora. Una fila de <see cref="Auditoria"/> por entidad creada,
/// modificada o borrada, en el MISMO SaveChanges que el cambio que la origina — asi
/// que comparte transaccion: o entran las dos cosas o no entra ninguna.
///
/// SOLO CUBRE LAS ACCIONES 1 A 3. Un interceptor de SaveChanges no ve mas que
/// escrituras; Acceso, Denegado, Exportacion, Login y LoginFallido no modifican ni
/// una fila y los escribe el caso de uso a mano. Esta clase no las conoce.
///
/// El mismo interceptor sirve a los dos contextos porque <see cref="Auditoria"/> esta
/// configurada en ambos con el mismo esquema y no tiene ni una relacion.
///
/// ponytail: lo que pasa por ExecuteUpdateAsync/ExecuteDeleteAsync NO SE AUDITA. Esas
/// dos no tocan el ChangeTracker: van directas a SQL, asi que ningun interceptor de
/// SaveChanges puede verlas. Hoy son 18 llamadas, todas en los caminos de Fase 0
/// —auth, aprovisionamiento, catalogo de planes—. Si alguna de esas escrituras tiene
/// que quedar registrada, el camino es cambiar ESA llamada a seguimiento normal, no
/// ensanchar el interceptor.
/// </summary>
internal sealed class InterceptorAuditoria(IContextoAuditoria actor) : SaveChangesInterceptor
{
    /// <summary>
    /// LA LISTA NO ES OPCIONAL. Esas columnas guardan hashes precisamente para que
    /// leer la base no de material usable, y meterlos en el jsonb lo desharia por la
    /// puerta de atras, en la tabla que nunca se borra.
    ///
    /// Por NOMBRE DE PROPIEDAD y no por par entidad-propiedad: cubre de una vez
    /// usuario.hash_contrasena, token_acceso.hash_token y sesion_refresh.hash_token, y
    /// —lo que importa mas— cubre tambien la proxima entidad que nazca con un campo
    /// que se llame igual, sin que nadie tenga que acordarse de ampliar la lista.
    /// </summary>
    private static readonly HashSet<string> Excluidas =
        new(StringComparer.Ordinal) { "HashContrasena", "HashToken" };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData datos, InterceptionResult<int> resultado)
    {
        Registrar(datos.Context);

        return base.SavingChanges(datos, resultado);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData datos,
        InterceptionResult<int> resultado,
        CancellationToken ct = default)
    {
        Registrar(datos.Context);

        return base.SavingChangesAsync(datos, resultado, ct);
    }

    /// <summary>
    /// internal y no private para que la prueba pueda llamarla con un contexto que
    /// nunca abre conexion. La alternativa era fabricar un DbContextEventData a mano,
    /// que es mas codigo de prueba y no comprueba nada mas.
    /// </summary>
    internal void Registrar(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        // El origen se decide AQUI y no en el middleware, porque depende de contra que
        // base se escribe: un superadministrador es 'plataforma' cuando actua dentro de
        // la base de una empresa —ahi su usuario_id no resuelve— y 'api' cuando actua
        // en la central, donde si resuelve.
        var origen = !actor.EstaEstablecido
            ? OrigenesAuditoria.Sistema
            : actor.EsPlataforma && contexto is ContextoEmpresa
                ? OrigenesAuditoria.Plataforma
                : OrigenesAuditoria.Api;

        // Se materializa ANTES de agregar nada: agregar filas mientras se recorre el
        // ChangeTracker lo invalida, y ademas auditariamos la propia auditoria.
        var cambios = contexto.ChangeTracker
            .Entries()
            .Where(e => e.Entity is not Auditoria)
            .Where(e => e.State is EntityState.Added
                or EntityState.Modified
                or EntityState.Deleted)
            .ToList();

        foreach (var entrada in cambios)
        {
            var fila = Describir(entrada, origen);

            if (fila is null)
            {
                continue;
            }

            fila.CorrelacionId = actor.CorrelacionId;
            fila.UsuarioId = actor.UsuarioId;
            fila.UsuarioCorreo = actor.UsuarioCorreo;
            fila.Roles = actor.Roles;
            fila.Ip = actor.Ip;

            contexto.Add(fila);
        }
    }

    /// <returns>null cuando no hay nada que registrar.</returns>
    private static Auditoria? Describir(EntityEntry entrada, string origen)
    {
        var propiedades = entrada.Properties
            .Where(p => !Excluidas.Contains(p.Metadata.Name))
            .ToList();

        switch (entrada.State)
        {
            case EntityState.Added:
                return new Auditoria
                {
                    Accion = AccionAuditoria.Alta,
                    Origen = origen,
                    Entidad = entrada.Metadata.ClrType.Name,
                    EntidadId = Llave(entrada),
                    ValoresNuevos = AJson(propiedades, p => p.CurrentValue),
                };

            case EntityState.Deleted:
                return new Auditoria
                {
                    Accion = AccionAuditoria.Borrado,
                    Origen = origen,
                    Entidad = entrada.Metadata.ClrType.Name,
                    EntidadId = Llave(entrada),
                    ValoresAnteriores = AJson(propiedades, p => p.OriginalValue),
                };

            default:
                var modificadas = propiedades.Where(p => p.IsModified).ToList();

                // Un SaveChanges puede traer entidades marcadas como modificadas cuyo
                // unico campo cambiado sea uno excluido, o ninguno. Una fila de bitacora
                // con dos jsonb vacios no dice nada y ademas mentiria: los dos nulos
                // significan uno de los eventos 4 a 8.
                if (modificadas.Count == 0)
                {
                    return null;
                }

                return new Auditoria
                {
                    Accion = AccionAuditoria.Cambio,
                    Origen = origen,
                    Entidad = entrada.Metadata.ClrType.Name,
                    EntidadId = Llave(entrada),
                    ValoresAnteriores = AJson(modificadas, p => p.OriginalValue),
                    ValoresNuevos = AJson(modificadas, p => p.CurrentValue),
                };
        }
    }

    /// <summary>
    /// La llave primaria como texto. Las compuestas —rol_permiso y usuario_rol, que son
    /// justo las tablas de permisos— se unen con ':', que es la razon documentada de que
    /// <see cref="Auditoria.EntidadId"/> sea text y no uuid.
    /// </summary>
    private static string Llave(EntityEntry entrada)
    {
        var llave = entrada.Metadata.FindPrimaryKey();

        if (llave is null)
        {
            return string.Empty;
        }

        return string.Join(
            ':',
            llave.Properties.Select(p => entrada.Property(p.Name).CurrentValue?.ToString()));
    }

    private static string AJson(
        IEnumerable<PropertyEntry> propiedades, Func<PropertyEntry, object?> valor)
        => JsonSerializer.Serialize(
            propiedades.ToDictionary(p => p.Metadata.Name, p => Normalizar(valor(p))));

    /// <summary>
    /// Lo que System.Text.Json no serializa por si solo, a texto.
    ///
    /// Los enums van como NUMERO y no como nombre, para que el jsonb diga lo mismo que
    /// la columna: renombrar un valor del enum en C# no debe cambiar lo que ya esta
    /// escrito en una bitacora que nunca se borra.
    /// </summary>
    private static object? Normalizar(object? valor) => valor switch
    {
        null => null,
        Enum e => Convert.ToInt64(e),
        byte[] b => Convert.ToBase64String(b),
        string or bool or byte or short or int or long or float or double or decimal
            or DateTime or DateOnly or TimeOnly or DateTimeOffset or Guid => valor,

        // IPAddress y lo que venga. ToString() antes que reventar dentro de un
        // SaveChanges: un tipo nuevo sin converter no puede tumbar la escritura de
        // negocio que lo origino.
        _ => valor.ToString(),
    };
}
