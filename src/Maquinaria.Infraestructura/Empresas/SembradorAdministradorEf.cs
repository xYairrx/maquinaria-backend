using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Seguridad;
using Maquinaria.Infraestructura.Correo;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maquinaria.Infraestructura.Empresas;

internal sealed class SembradorAdministradorEf(
    ProveedorContextoEmpresa proveedor,
    IGeneradorTokens tokens,
    IOptions<OpcionesCorreo> correo,
    ILogger<SembradorAdministradorEf> log) : ISembradorAdministrador
{
    public async Task<AdministradorSembrado> CrearAdministradorAsync(
        string nombreBd, string correoUsuario, string nombre, CancellationToken ct)
    {
        var normalizado = correoUsuario.Trim().ToLowerInvariant();

        await using var contexto = proveedor.ParaMigrar(nombreBd);

        // ---------- si ya hay alguien con acceso total, ES ESE ----------
        // El reintento de un alta que fallo DESPUES de este paso vuelve a pasar por aqui
        // con el correo que capture quien lo dispare, y si fuera otro se crearia una
        // SEGUNDA cuenta con acceso total — una que nadie pidio y que no aparece en la
        // interfaz de asignaciones, porque el rol administrador no se asigna desde ahi.
        //
        // Asi que gana el que ya esta: el correo recibido se ignora y lo que hace el
        // reintento es reemitirle SU invitacion. La empresa sigue teniendo exactamente
        // una persona con acceso total, que es la garantia que este flujo sostiene.
        var conAccesoTotal = await contexto.UsuarioRoles
            .Join(contexto.Roles, ur => ur.RolId, r => r.Id, (ur, r) => new { ur.UsuarioId, r.AccesoTotal })
            .Where(x => x.AccesoTotal)
            .Select(x => x.UsuarioId)
            .FirstOrDefaultAsync(ct);

        if (conAccesoTotal != Guid.Empty)
        {
            var existente = await contexto.Usuarios
                .FirstAsync(u => u.Id == conAccesoTotal, ct);

            if (existente.Correo != normalizado)
            {
                log.LogWarning(
                    "{NombreBd} ya tiene administrador con acceso total. Se reemite su "
                    + "invitacion y se ignora el correo recibido.",
                    nombreBd);
            }

            normalizado = existente.Correo;
        }

        // ---------- el usuario ----------
        // IDEMPOTENTE: si un alta anterior fallo despues de este paso, reintentar no
        // debe duplicarlo. usuario_correo_unico lo impediria de todas formas, pero
        // reventar no es el comportamiento que queremos al reintentar.
        var usuario = await contexto.Usuarios
            .FirstOrDefaultAsync(u => u.Correo == normalizado, ct);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                Correo = normalizado,
                Nombre = nombre,

                // Invitado, sin hash de contrasena. No hay registro publico: la persona
                // define su contrasena al abrir la liga, y hasta entonces no puede entrar.
                Estado = EstadoUsuario.Invitado,
            };

            contexto.Usuarios.Add(usuario);
            await contexto.SaveChangesAsync(ct);
        }

        // ---------- el rol ----------
        // ESTE ES EL UNICO LUGAR donde se asigna 'administrador'. No aparece en la
        // interfaz de asignaciones, asi que la empresa tendra exactamente una persona
        // con acceso total, y si esa persona se va solo la plataforma puede nombrar otra.
        var rolAdmin = await contexto.Roles
            .FirstOrDefaultAsync(r => r.Codigo == ClavesRol.Administrador, ct)
            ?? throw new InvalidOperationException(
                $"La base {nombreBd} no tiene el rol '{ClavesRol.Administrador}'. "
                + "Sus migraciones no se aplicaron completas.");

        var yaTieneRol = await contexto.UsuarioRoles
            .AnyAsync(ur => ur.UsuarioId == usuario.Id && ur.RolId == rolAdmin.Id, ct);

        if (!yaTieneRol)
        {
            contexto.UsuarioRoles.Add(new UsuarioRol
            {
                UsuarioId = usuario.Id,
                RolId = rolAdmin.Id,
            });
        }

        // ---------- la invitacion ----------
        // Se invalidan las pendientes ANTES de emitir la nueva: reintentar un alta no
        // debe dejar dos ligas validas circulando.
        var invalidadas = await contexto.TokensAcceso
            .Where(t => t.UsuarioId == usuario.Id
                && t.Proposito == PropositoToken.Invitacion
                && t.UsadoEn == null
                && t.InvalidadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.InvalidadoEn, DateTime.UtcNow), ct);

        if (invalidadas > 0)
        {
            log.LogWarning(
                "Se invalidaron {Cuantas} invitaciones pendientes de {Correo} al reemitir.",
                invalidadas, normalizado);
        }

        var token = tokens.Generar();

        contexto.TokensAcceso.Add(new TokenAcceso
        {
            UsuarioId = usuario.Id,
            Proposito = PropositoToken.Invitacion,

            // Se guarda el HASH. Leer la base no debe dar ligas usables.
            HashToken = token.Hash,

            ExpiraEn = DateTime.UtcNow.AddDays(correo.Value.DiasVigenciaInvitacion),

            // NULL significa "la creo la plataforma". El superadministrador que dio de
            // alta la empresa vive en la base central y NO EXISTE en esta base, asi que
            // no hay id que poner.
            CreadoPorId = null,
        });

        await contexto.SaveChangesAsync(ct);

        log.LogInformation(
            "Administrador {Correo} creado en {NombreBd} con invitacion vigente.",
            normalizado, nombreBd);

        // El token EN CLARO se devuelve y no se guarda: es el unico momento en que
        // existe. Y con el va el correo que DE VERDAD se sembro, que es a donde tiene que
        // ir la liga.
        return new AdministradorSembrado(normalizado, token.EnClaro);
    }
}
