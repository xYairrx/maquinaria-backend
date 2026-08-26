using Maquinaria.Dominio.Organizacion;

namespace Maquinaria.Aplicacion.Organizacion;

/// <summary>
/// Una ubicacion fisica: bodega, sucursal o patio. **Los tres al mismo nivel, no una
/// jerarquia.**
///
/// <c>AlmacenaEquipo</c> y <c>EsAdministrativa</c> SE DERIVAN DEL TIPO y no se capturan: en la
/// base son columnas generadas por Postgres, y aqui son propiedades calculadas del DTO. Con
/// banderas capturables se podria crear una «bodega que cotiza», que no existe.
///
/// Tres reglas del motor dependen de ellas: un equipo solo vive donde se almacena, un traspaso
/// va de almacen a almacen, y una cotizacion sale de una ubicacion administrativa.
/// </summary>
public sealed record UbicacionDto(
    Guid Id,
    string Codigo,
    string Nombre,
    TipoUbicacion Tipo,
    string? Domicilio,
    string? Telefono,
    decimal? Latitud,
    decimal? Longitud,
    bool Activo,
    int Equipos)
{
    public bool AlmacenaEquipo => Tipo is TipoUbicacion.Bodega or TipoUbicacion.Patio;

    public bool EsAdministrativa => Tipo is TipoUbicacion.Sucursal or TipoUbicacion.Patio;
}

/// <summary>
/// El alta NO acepta las dos capacidades, solo el tipo. Es la mitad de aplicacion de la
/// garantia que la base impone con columnas generadas.
/// </summary>
public readonly record struct AltaUbicacion(
    string Codigo,
    string Nombre,
    TipoUbicacion Tipo,
    string? Domicilio,
    string? Telefono,
    decimal? Latitud,
    decimal? Longitud);

public sealed record FiltroUbicaciones : Comun.Filtro
{
    public TipoUbicacion? Tipo { get; init; }

    /// <summary>
    /// Solo las que guardan maquinas —bodega y patio—. Lo pide la pantalla de alta de equipo
    /// y la de traspasos, que no deben ofrecer sucursales.
    /// </summary>
    public bool? AlmacenaEquipo { get; init; }

    /// <summary>Solo las que cotizan —sucursal y patio—.</summary>
    public bool? EsAdministrativa { get; init; }
}
