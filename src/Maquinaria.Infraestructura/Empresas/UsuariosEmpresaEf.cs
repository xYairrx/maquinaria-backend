using Maquinaria.Aplicacion.Empresas;
using Maquinaria.Dominio.Seguridad;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Empresas;

internal sealed class UsuariosEmpresaEf(ContextoEmpresa empresa) : IUsuariosEmpresa
{
    public async Task<TokenConUsuario?> BuscarTokenVigenteAsync(
        string hashToken, PropositoToken proposito, CancellationToken ct)
    {
        // La vigencia se comprueba EN LA CONSULTA y no despues: asi no hay una rama de
        // codigo donde un token caducado ya este cargado y alguien lo use por descuido.
        //
        // La condicion viene de TokenAcceso.Vigente y no esta escrita aqui: es la misma
        // que comprueban las pruebas, y con dos copias una se queda atras.
        var token = await empresa.TokensAcceso
            .Include(t => t.Usuario)
            .Where(t => t.HashToken == hashToken)
            .Where(TokenAcceso.Vigente(proposito, DateTime.UtcNow))
            .FirstOrDefaultAsync(ct);

        return token?.Usuario is null ? null : new TokenConUsuario(token, token.Usuario);
    }

    public async Task EmitirTokenAsync(
        Guid usuarioId, PropositoToken proposito, string hashToken, DateTime expiraEn,
        CancellationToken ct)
    {
        // TRANSACCION EXPLICITA por lo mismo que en AceptarInvitacionAsync: el
        // ExecuteUpdate no pasa por SaveChanges y no comparte transaccion implicita con
        // el insert. Sin esto se puede invalidar la liga anterior y no llegar a emitir la
        // nueva, que deja a la persona sin ninguna.
        await using var transaccion = await empresa.Database.BeginTransactionAsync(ct);

        await empresa.TokensAcceso
            .Where(t => t.UsuarioId == usuarioId
                && t.Proposito == proposito
                && t.UsadoEn == null
                && t.InvalidadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.InvalidadoEn, DateTime.UtcNow), ct);

        empresa.TokensAcceso.Add(new TokenAcceso
        {
            UsuarioId = usuarioId,
            Proposito = proposito,

            // Se guarda el HASH. Leer la base no debe dar ligas usables.
            HashToken = hashToken,
            ExpiraEn = expiraEn,

            // NULL significa "no la creo un usuario de la empresa". Un restablecimiento
            // lo pide el propio interesado SIN SESION, asi que no hay id que registrar:
            // el unico dato que tenemos de quien lo pidio es una IP, y esa va al log.
            CreadoPorId = null,
        });

        await empresa.SaveChangesAsync(ct);

        await transaccion.CommitAsync(ct);
    }

    public async Task RestablecerContrasenaAsync(
        Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct)
    {
        await using var transaccion = await empresa.Database.BeginTransactionAsync(ct);

        // SIN tocar Estado: quien restablece ya estaba Activo. Ponerlo aqui, copiando de
        // AceptarInvitacionAsync, convertiria una liga vieja en una forma de resucitar a
        // un usuario suspendido o dado de baja.
        await empresa.Usuarios
            .Where(u => u.Id == usuarioId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.HashContrasena, hashContrasena)
                      .SetProperty(u => u.DebeCambiarContrasena, false)
                      .SetProperty(u => u.ActualizadoEn, DateTime.UtcNow),
                ct);

        await empresa.TokensAcceso
            .Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEn, DateTime.UtcNow), ct);

        // Se reusa el metodo de la deteccion de reuso en lugar de repetir el
        // ExecuteUpdate: es el mismo contexto, asi que entra en la transaccion abierta.
        //
        // Va DENTRO de la transaccion a proposito. Si se revocara despues del commit y el
        // proceso se cayera en medio, la contrasena quedaria cambiada con las sesiones del
        // atacante vivas, que es exactamente el escenario que este flujo existe para
        // cerrar.
        await RevocarSesionesDeAsync(usuarioId, ct);

        await transaccion.CommitAsync(ct);
    }

    public async Task AceptarInvitacionAsync(
        Guid usuarioId, Guid tokenId, string hashContrasena, CancellationToken ct)
    {
        // TRANSACCION EXPLICITA. Son dos ExecuteUpdate, que no pasan por SaveChanges y
        // por tanto no comparten transaccion implicita. Sin esto, la contrasena podria
        // guardarse sin quemar el token —liga reusable— o quemarse sin guardar la
        // contrasena —cuenta inaccesible—.
        await using var transaccion = await empresa.Database.BeginTransactionAsync(ct);

        await empresa.Usuarios
            .Where(u => u.Id == usuarioId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.HashContrasena, hashContrasena)
                      .SetProperty(u => u.Estado, EstadoUsuario.Activo)
                      .SetProperty(u => u.DebeCambiarContrasena, false)
                      .SetProperty(u => u.ActualizadoEn, DateTime.UtcNow),
                ct);

        await empresa.TokensAcceso
            .Where(t => t.Id == tokenId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEn, DateTime.UtcNow), ct);

        await transaccion.CommitAsync(ct);
    }

    public Task<Usuario?> BuscarPorCorreoAsync(string correo, CancellationToken ct)
        => empresa.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo, ct);

    public async Task<IReadOnlyList<string>> PermisosDeAsync(Guid usuarioId, CancellationToken ct)
        => await empresa.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId)
            .Join(empresa.RolPermisos, ur => ur.RolId, rp => rp.RolId, (ur, rp) => rp.PermisoId)
            .Join(empresa.Permisos, id => id, p => p.Id, (id, p) => p.Clave)
            .Distinct()
            .ToListAsync(ct);

    public Task<bool> TieneAccesoTotalAsync(Guid usuarioId, CancellationToken ct)
        => empresa.UsuarioRoles
            .Where(ur => ur.UsuarioId == usuarioId)
            .Join(empresa.Roles, ur => ur.RolId, r => r.Id, (ur, r) => r)
            .AnyAsync(r => r.AccesoTotal, ct);

    public async Task RegistrarAccesoAsync(
        Guid usuarioId, DateTime cuandoUtc, string? hashNuevo, CancellationToken ct)
    {
        if (hashNuevo is null)
        {
            await empresa.Usuarios
                .Where(u => u.Id == usuarioId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.UltimoAccesoEn, cuandoUtc), ct);

            return;
        }

        await empresa.Usuarios
            .Where(u => u.Id == usuarioId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.UltimoAccesoEn, cuandoUtc)
                      .SetProperty(u => u.HashContrasena, hashNuevo),
                ct);
    }

    public async Task CrearSesionAsync(SesionRefresh sesion, CancellationToken ct)
    {
        empresa.SesionesRefresh.Add(sesion);
        await empresa.SaveChangesAsync(ct);
    }

    public Task<SesionRefresh?> BuscarSesionPorHashAsync(string hashToken, CancellationToken ct)
        // SIN filtrar por revocada ni por reemplazada: el caso de uso NECESITA ver una
        // sesion ya reemplazada, porque encontrarla es justo la senal de reuso.
        => empresa.SesionesRefresh.FirstOrDefaultAsync(s => s.HashToken == hashToken, ct);

    public async Task RotarSesionAsync(Guid anteriorId, SesionRefresh nueva, CancellationToken ct)
    {
        await using var transaccion = await empresa.Database.BeginTransactionAsync(ct);

        empresa.SesionesRefresh.Add(nueva);
        await empresa.SaveChangesAsync(ct);

        // El enlace se escribe DESPUES de insertar la nueva, porque reemplazado_por_id
        // tiene FK a sesion_refresh: apuntar a una fila que no existe fallaria.
        await empresa.SesionesRefresh
            .Where(s => s.Id == anteriorId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ReemplazadoPorId, nueva.Id)
                      .SetProperty(x => x.RevocadoEn, DateTime.UtcNow),
                ct);

        await transaccion.CommitAsync(ct);
    }

    public async Task RevocarSesionesDeAsync(Guid usuarioId, CancellationToken ct)
        => await empresa.SesionesRefresh
            .Where(s => s.UsuarioId == usuarioId && s.RevocadoEn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevocadoEn, DateTime.UtcNow), ct);
}
