using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Terceros;

/// <summary>Los proveedores. Sin borrado: las ordenes de compra los referencian.</summary>
public interface IServicioProveedores
{
    Task<Pagina<ProveedorDto>> ListarAsync(Filtro filtro, CancellationToken ct);

    Task<ProveedorDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<ProveedorDto>> CrearAsync(AltaProveedor alta, CancellationToken ct);

    Task<Resultado<ProveedorDto>> EditarAsync(
        Guid id, AltaProveedor cambio, CancellationToken ct);

    Task<Resultado<ProveedorDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct);
}
