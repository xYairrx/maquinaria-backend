using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// Una tarifa NO es el precio de rentar un equipo: es un CONCEPTO COBRABLE —renta diaria,
/// flete, operador, maniobras, mantenimiento— y una renta o una venta arrastra varios.
///
/// El precio no vive aqui. Vive en <c>equipo_tarifa</c>, por equipo y con vigencia, porque
/// cambia con el tiempo y un cliente grande negocia el suyo.
/// </summary>
/// <param name="Unidad">
/// Hora, Dia, Semana, Mes, Evento o Kilometro. Es lo que dice si el precio se multiplica o se
/// cobra una vez: sin ella, «flete: 3500» es ambiguo.
/// </param>
public sealed record TarifaDto(
    Guid Id,
    string Codigo,
    string Nombre,
    string? Descripcion,
    UnidadTarifa Unidad,
    bool AplicaRenta,
    bool AplicaVenta,
    bool Activo);

public readonly record struct AltaTarifa(
    string Codigo,
    string Nombre,
    string? Descripcion,
    UnidadTarifa Unidad,
    bool AplicaRenta,
    bool AplicaVenta);

/// <summary>
/// Filtra por donde aplica, que es lo que necesitan las pantallas de captura: al armar una
/// renta solo se ofrecen las de renta, y al armar una venta solo las de venta.
/// </summary>
public sealed record FiltroTarifas : Comun.Filtro
{
    public bool? AplicaRenta { get; init; }

    public bool? AplicaVenta { get; init; }

    public UnidadTarifa? Unidad { get; init; }
}
