namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// El catalogo de TIPOS de limite: que limites sabe nombrar el sistema.
///
/// Separado de <see cref="ILimitesTenant"/> con el mismo criterio que separa
/// <see cref="ICatalogoPlanes"/> del registro de tenants: aqui se administra el catalogo
/// —se define una vez y se consulta mucho—, y alla se ajusta el cupo de UNA empresa, que es
/// una negociacion comercial y pasa a menudo.
///
/// LA ADVERTENCIA MAS IMPORTANTE DE ESTE ARCHIVO, que <c>TipoLimite</c> ya trae escrita:
/// crear un tipo NO crea un limite. Un limite solo acota cuando hay codigo que lo lee y
/// bloquea la operacion, y ese codigo busca las claves de <c>ClavesLimite</c>. Un tipo con
/// una clave inventada es una fila con un nombre bonito: se puede crear, editar y fijar por
/// empresa, y no va a impedir nada nunca. Por eso <c>ResumenTipoLimite.Reconocida</c> viaja
/// hasta la pantalla — para que se vea, en lugar de descubrirse el dia que el cupo no se
/// respete.
/// </summary>
public interface ICatalogoLimites
{
    /// <summary>
    /// Todos los tipos, ACTIVOS E INACTIVOS, ordenados por <c>Orden</c>.
    ///
    /// Los inactivos se incluyen por lo mismo que los planes retirados: el panel es donde se
    /// administra el catalogo, y un tipo retirado hay que poder verlo para reactivarlo. Quien
    /// filtra por activo es la resolucion de cupos de una empresa, no esta lista.
    /// </summary>
    Task<IReadOnlyList<ResumenTipoLimite>> ListarAsync(CancellationToken ct);

    Task<ResultadoTipoLimite> CrearAsync(AltaTipoLimite alta, CancellationToken ct);

    /// <summary>
    /// Edita los campos editables. La clave no es uno de ellos.
    ///
    /// OJO CON <c>ValorDefecto</c>: cambiarlo mueve el cupo efectivo de TODAS las empresas
    /// que no tengan excepcion, de golpe. Es lo que un valor por defecto significa, y es
    /// justo por lo que la pantalla tiene que decir cuantas empresas quedan afectadas.
    ///
    /// Devuelve un resultado rechazado si la clave no existe.
    /// </summary>
    Task<ResultadoTipoLimite> EditarAsync(
        string clave, CambioTipoLimite cambio, CancellationToken ct);
}
