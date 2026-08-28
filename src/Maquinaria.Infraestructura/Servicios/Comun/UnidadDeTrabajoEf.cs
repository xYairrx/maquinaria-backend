using Maquinaria.Aplicacion.Comun;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Maquinaria.Infraestructura.Servicios.Comun;

/// <summary>
/// La transaccion, sobre el <see cref="ContextoEmpresa"/> de la peticion.
///
/// Funciona porque el contexto es SCOPED: el Proceso y todos los Servicios que compone
/// comparten la misma instancia, asi que todos escriben dentro de la transaccion que se abre
/// aqui sin tener que enterarse.
/// </summary>
internal sealed class UnidadDeTrabajoEf(ContextoEmpresa bd) : IUnidadDeTrabajo
{
    public async Task<ITransaccion> IniciarAsync(CancellationToken ct)
        => new TransaccionEf(await bd.Database.BeginTransactionAsync(ct));

    private sealed class TransaccionEf(IDbContextTransaction transaccion) : ITransaccion
    {
        public Task ConfirmarAsync(CancellationToken ct) => transaccion.CommitAsync(ct);

        /// <summary>
        /// Desechar sin confirmar deshace. No hace falta un Rollback explicito: EF lo hace al
        /// desechar una transaccion no confirmada, y depender de eso es lo que vuelve seguro
        /// el <c>await using</c> con `return` a media funcion.
        /// </summary>
        public ValueTask DisposeAsync() => transaccion.DisposeAsync();
    }
}
