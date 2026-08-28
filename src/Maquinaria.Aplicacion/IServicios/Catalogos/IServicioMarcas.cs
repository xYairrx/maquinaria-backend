using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// El catalogo de marcas. Sin borrado, con desactivacion, por la misma razon que
/// <see cref="IServicioCategoriasEquipo"/>: no hay <c>eliminado_en</c> y <c>modelo_equipo</c>
/// la referencia.
/// </summary>
public interface IServicioMarcas
{
    Task<Pagina<MarcaDto>> ListarAsync(Filtro filtro, CancellationToken ct);

    Task<MarcaDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<MarcaDto>> CrearAsync(AltaMarca alta, CancellationToken ct);

    Task<Resultado<MarcaDto>> EditarAsync(Guid id, AltaMarca cambio, CancellationToken ct);

    Task<Resultado<MarcaDto>> CambiarActivoAsync(Guid id, bool activo, CancellationToken ct);
}
