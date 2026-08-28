using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// El catalogo de modelos. Su clave unica es <c>(marca, nombre)</c> y no el nombre solo: «300»
/// puede existir en dos marcas y son dos modelos distintos.
/// </summary>
public interface IServicioModelosEquipo
{
    Task<Pagina<ModeloEquipoDto>> ListarAsync(FiltroModelosEquipo filtro, CancellationToken ct);

    Task<ModeloEquipoDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<ModeloEquipoDto>> CrearAsync(AltaModeloEquipo alta, CancellationToken ct);

    Task<Resultado<ModeloEquipoDto>> EditarAsync(
        Guid id, AltaModeloEquipo cambio, CancellationToken ct);

    Task<Resultado<ModeloEquipoDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct);
}
