using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>El catalogo de puestos. Sin borrado: <c>trabajador.puesto_id</c> los referencia.</summary>
public interface IServicioPuestos
{
    Task<Pagina<PuestoDto>> ListarAsync(Filtro filtro, CancellationToken ct);

    Task<PuestoDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<PuestoDto>> CrearAsync(AltaPuesto alta, CancellationToken ct);

    Task<Resultado<PuestoDto>> EditarAsync(Guid id, AltaPuesto cambio, CancellationToken ct);

    Task<Resultado<PuestoDto>> CambiarActivoAsync(Guid id, bool activo, CancellationToken ct);
}
