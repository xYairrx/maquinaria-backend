using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Activos;

namespace Maquinaria.Aplicacion.Equipos;

/// <summary>
/// El parque de equipos.
///
/// ES UNA DE LAS TRES ENTIDADES CON BORRADO LOGICO —con <c>archivo</c> y <c>tenant</c>—, asi
/// que aqui si hay borrado de verdad: <c>eliminado_en</c>, y <c>Filtro.IncluirEliminados</c>
/// funciona. Los catalogos no lo tienen y se retiran con activo.
///
/// EL ESTADO NO SE MUEVE A MANO A CUALQUIER PARTE. Rentado, Reservado y Vendido los pone un
/// Proceso —confirmar una renta, finalizar una venta— porque son consecuencia de un documento,
/// no una decision de captura. Ponerlos a mano dejaria el calendario y el estado diciendo
/// cosas distintas.
/// </summary>
public interface IServicioEquipos
{
    Task<Pagina<EquipoDto>> ListarAsync(FiltroEquipos filtro, CancellationToken ct);

    Task<EquipoDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<EquipoDto>> CrearAsync(AltaEquipo alta, CancellationToken ct);

    Task<Resultado<EquipoDto>> EditarAsync(Guid id, AltaEquipo cambio, CancellationToken ct);

    /// <summary>
    /// Disponible, EnMantenimiento, FueraDeServicio o Baja. Los tres estados que dependen de
    /// un documento se rechazan.
    /// </summary>
    Task<Resultado<EquipoDto>> CambiarEstadoAsync(
        Guid id, CambioEstadoEquipo cambio, CancellationToken ct);

    /// <summary>
    /// Borrado logico. Se rechaza si el equipo tiene calendario ocupado: un equipo eliminado
    /// con una renta activa desaparece de las listas mientras sigue en la obra.
    /// </summary>
    Task<Resultado> EliminarAsync(Guid id, CancellationToken ct);
}
