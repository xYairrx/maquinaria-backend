using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Terceros;

namespace Maquinaria.Aplicacion.Terceros;

/// <summary>
/// Los clientes.
///
/// SIN BORRADO: <c>cliente</c> no tiene <c>eliminado_en</c> y lo referencian cotizaciones,
/// rentas, contratos, ordenes de venta y precios negociados. Retirar a un cliente es ponerlo
/// en Suspendido o Baja.
/// </summary>
public interface IServicioClientes
{
    Task<Pagina<ClienteDto>> ListarAsync(FiltroClientes filtro, CancellationToken ct);

    Task<ClienteDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<ClienteDto>> CrearAsync(AltaCliente alta, CancellationToken ct);

    Task<Resultado<ClienteDto>> EditarAsync(Guid id, AltaCliente cambio, CancellationToken ct);

    Task<Resultado<ClienteDto>> CambiarEstadoAsync(
        Guid id, EstadoCliente estado, CancellationToken ct);
}
