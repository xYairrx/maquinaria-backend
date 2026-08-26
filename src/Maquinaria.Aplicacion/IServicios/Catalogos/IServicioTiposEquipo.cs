using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// El catalogo de tipos de equipo. Sin borrado, con desactivacion, como el resto de los
/// catalogos: no hay <c>eliminado_en</c> y <c>equipo.tipo_equipo_id</c> los referencia.
/// </summary>
public interface IServicioTiposEquipo
{
    Task<Pagina<TipoEquipoDto>> ListarAsync(FiltroTiposEquipo filtro, CancellationToken ct);

    Task<TipoEquipoDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<TipoEquipoDto>> CrearAsync(AltaTipoEquipo alta, CancellationToken ct);

    Task<Resultado<TipoEquipoDto>> EditarAsync(
        Guid id, AltaTipoEquipo cambio, CancellationToken ct);

    Task<Resultado<TipoEquipoDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct);
}
