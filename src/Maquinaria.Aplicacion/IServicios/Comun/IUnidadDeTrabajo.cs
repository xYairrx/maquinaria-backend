namespace Maquinaria.Aplicacion.Comun;

/// <summary>
/// Una transaccion sobre la base de la empresa, para los Procesos que escriben en dos o mas
/// tablas y necesitan que sea todo o nada.
///
/// EXISTE POR LA REGLA DE CAPAS. Un Proceso vive en <c>Aplicacion</c>, que no referencia EF
/// Core, asi que no puede llamar a <c>Database.BeginTransactionAsync</c>. Sin esta abstraccion
/// las dos salidas serian malas: mover los Procesos a Infraestructura —y perder la frontera— o
/// dejar que cada Servicio confirme por su cuenta, que es exactamente lo que hace imposible
/// deshacer una renta a medio confirmar.
///
/// **No es un patron Repository ni una unidad de trabajo completa.** No expone `SaveChanges`:
/// cada Servicio sigue guardando lo suyo. Lo unico que agrega es el ambito transaccional que
/// los envuelve.
/// </summary>
public interface IUnidadDeTrabajo
{
    Task<ITransaccion> IniciarAsync(CancellationToken ct);
}

/// <summary>
/// El ambito. Si se desecha sin confirmar, se deshace: el <c>await using</c> del Proceso es lo
/// que garantiza que un rechazo a media escritura no deje nada.
/// </summary>
public interface ITransaccion : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken ct);
}
