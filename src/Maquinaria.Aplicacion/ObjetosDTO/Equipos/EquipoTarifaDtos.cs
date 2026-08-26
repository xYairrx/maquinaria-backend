namespace Maquinaria.Aplicacion.Equipos;

/// <summary>
/// El precio de un concepto para un equipo, con vigencia.
///
/// **AQUI VIVE EL PRECIO, no en el catalogo de tarifas.** El catalogo dice QUE se cobra —renta
/// diaria, flete, operador—; esta tabla dice CUANTO, por equipo, con fecha, y opcionalmente
/// para un cliente en concreto.
/// </summary>
/// <param name="ClienteId">
/// Nulo es el precio de lista. Con cliente es el precio negociado con ese cliente, y gana sobre
/// el de lista. Un `EXCLUDE` impide que existan dos vigentes para la misma combinacion.
/// </param>
public sealed record EquipoTarifaDto(
    Guid Id,
    Guid EquipoId,
    Guid TarifaId,
    string Tarifa,
    string Unidad,
    Guid? ClienteId,
    string? Cliente,
    decimal Precio,
    string Moneda,
    DateTime VigenciaDesde,
    DateTime? VigenciaHasta)
{
    /// <summary>Si hoy cae dentro de la vigencia. Lo que la pantalla pinta en verde.</summary>
    public bool Vigente => VigenciaHasta == null || VigenciaHasta > DateTime.UtcNow;
}

/// <param name="VigenciaHasta">
/// Nulo es «hasta nuevo aviso». Es lo normal al cargar el precio de lista, y es lo que hace que
/// cargar el siguiente exija cerrar el anterior.
/// </param>
public readonly record struct AltaEquipoTarifa(
    Guid TarifaId,
    Guid? ClienteId,
    decimal Precio,
    string? Moneda,
    DateTime VigenciaDesde,
    DateTime? VigenciaHasta);
