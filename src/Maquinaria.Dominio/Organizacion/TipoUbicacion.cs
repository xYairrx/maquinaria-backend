namespace Maquinaria.Dominio.Organizacion;

/// <summary>
/// Que clase de sitio es una <see cref="Ubicacion"/>.
///
/// CORREGIDO EL 2026-08-24. La primera version modelaba sucursal como PADRE y ubicacion
/// como hija, y estaba mal: el negocio las define como tres tipos de sitio AL MISMO
/// NIVEL, y se distinguen por lo que se puede hacer en cada uno.
///
///     bodega     guarda maquinas
///     sucursal   administra y cotiza
///     patio      las dos cosas
///
/// Las dos capacidades se DERIVAN del tipo en lugar de guardarse como banderas. Con
/// banderas se podria crear una "bodega que cotiza", que no existe; derivandolas, esa
/// fila es imposible de escribir.
/// </summary>
public enum TipoUbicacion : short
{
    /// <summary>Solo resguardo de equipo. No se cotiza desde aqui.</summary>
    Bodega = 1,

    /// <summary>Solo administracion y comercial. Aqui no se guarda equipo.</summary>
    Sucursal = 2,

    /// <summary>Las dos cosas: guarda equipo y ademas administra y cotiza.</summary>
    Patio = 3,
}
