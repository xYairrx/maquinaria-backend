using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// El catalogo de conceptos cobrables. Lo administra rentas, no equipos: sus permisos son
/// <c>rentas.*</c>.
/// </summary>
public interface IServicioTarifas
{
    Task<Pagina<TarifaDto>> ListarAsync(FiltroTarifas filtro, CancellationToken ct);

    Task<TarifaDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<TarifaDto>> CrearAsync(AltaTarifa alta, CancellationToken ct);

    Task<Resultado<TarifaDto>> EditarAsync(Guid id, AltaTarifa cambio, CancellationToken ct);

    Task<Resultado<TarifaDto>> CambiarActivoAsync(Guid id, bool activo, CancellationToken ct);
}
