using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Rentas;

/// <summary>
/// Rentas, sus lineas y sus conceptos. **Solo la base de datos**: el calendario lo mueven los
/// Procesos, que son los unicos que pueden hacerlo dentro de una transaccion junto con la renta.
///
/// LOS ESTADOS DE LA FASE SON SEIS de los diez del enum: Borrador → Confirmada → Activa →
/// Devuelta → Cerrada, mas Cancelada. <c>PorEntregar</c> y <c>EnTraslado</c> son logistica —M8,
/// Fase 2— y <c>PorVencer</c> y <c>Vencida</c> **se derivan de la fecha** en el DTO en lugar de
/// guardarse, porque guardarlos exigiria un proceso nocturno que los mantuviera al dia.
///
/// LAS LINEAS SOLO SE TOCAN EN BORRADOR. Despues de confirmar, cada linea tiene una fila de
/// calendario detras: agregar una sin pasar por el Proceso dejaria un equipo rentado sin
/// ocupacion, que es exactamente el agujero que la fase existe para cerrar.
/// </summary>
public interface IServicioRentas
{
    Task<Pagina<RentaDto>> ListarAsync(FiltroRentas filtro, CancellationToken ct);

    Task<RentaDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ExtensionRentaDto>> ExtensionesAsync(Guid id, CancellationToken ct);

    Task<Resultado<RentaDto>> CrearAsync(AltaRenta alta, CancellationToken ct);

    Task<Resultado<RentaDto>> EditarAsync(Guid id, AltaRenta cambio, CancellationToken ct);

    Task<Resultado<RentaLineaDto>> AgregarLineaAsync(
        Guid rentaId, AltaRentaLinea linea, CancellationToken ct);

    Task<Resultado> QuitarLineaAsync(Guid rentaId, Guid lineaId, CancellationToken ct);

    Task<Resultado<RentaConceptoDto>> AgregarConceptoAsync(
        Guid rentaId, AltaRentaConcepto concepto, CancellationToken ct);

    Task<Resultado> QuitarConceptoAsync(Guid rentaId, Guid conceptoId, CancellationToken ct);

    // ---------------------------------------------------------------- para los Procesos ----

    /// <summary>
    /// Cambia el estado sin tocar el calendario. Lo llaman los Procesos dentro de su
    /// transaccion, y el controlador para los pasos que no mueven nada —Confirmada → Activa,
    /// Activa → Devuelta—.
    /// </summary>
    Task<Resultado<RentaDto>> CambiarEstadoAsync(
        Guid id, EstadoRenta estado, CancellationToken ct);

    /// <summary>Los equipos y el periodo de la renta, que es lo que el Proceso necesita para ocupar.</summary>
    Task<DatosParaOcupar?> DatosParaOcuparAsync(Guid id, CancellationToken ct);

    Task<Resultado> MoverFinAsync(Guid id, DateTime finNuevo, CancellationToken ct);

    Task<Resultado<ExtensionRentaDto>> RegistrarExtensionAsync(
        Guid id, AltaExtension alta, CancellationToken ct);

    Task<Resultado> RegistrarDevolucionAsync(
        Guid id, CierreDeRenta cierre, CancellationToken ct);
}

/// <param name="EquipoIds">Uno por <c>renta_linea</c>: son las filas de calendario que hay que crear.</param>
public sealed record DatosParaOcupar(
    Guid Id,
    string Folio,
    EstadoRenta Estado,
    DateTime Inicio,
    DateTime Fin,
    IReadOnlyList<Guid> EquipoIds);
