namespace Maquinaria.Aplicacion.Comun;

/// <summary>
/// Lo que todo listado acepta: buscar, paginar y ordenar.
///
/// ES UNA CLASE BASE Y NO UN TIPO CERRADO. Cada modulo hereda y agrega lo suyo
/// —<c>FiltroEquipos</c> con <c>CategoriaId</c> y <c>UbicacionId</c>, <c>FiltroRentas</c>
/// con el rango de fechas— y no vuelve a declarar la paginacion. Con un tipo por modulo
/// desde cero, la tercera pantalla ya tendria tres formas distintas de decir "pagina 2".
///
/// Se enlaza con <c>[FromQuery]</c>, asi que estos nombres son los de la cadena de
/// consulta: <c>?texto=cat&amp;numero=2&amp;tamano=50</c>.
/// </summary>
public record Filtro
{
    /// <summary>
    /// El techo del tamano de pagina, y la unica razon de que exista: sin el,
    /// <c>?tamano=1000000</c> es una consulta que trae la tabla entera y tumba la
    /// respuesta. No es una preferencia de interfaz, es la defensa del servidor.
    /// </summary>
    public const int TamanoMaximo = 200;

    public const int TamanoPorDefecto = 50;

    /// <summary>
    /// Busqueda libre. Cada Servicio decide sobre que columnas aplica —normalmente con
    /// <c>pg_trgm</c>— porque "texto" no significa lo mismo en equipos que en rentas.
    /// </summary>
    public string? Texto { get; init; }

    /// <summary>Nulo trae activos e inactivos; es distinto de <c>false</c>.</summary>
    public bool? Activo { get; init; }

    /// <summary>
    /// El borrado es logico, asi que las filas con <c>eliminado_en</c> siguen ahi. El
    /// listado las esconde SIEMPRE salvo que se pida esto, y pedirlo exige el permiso
    /// <c>.eliminar</c> del modulo: quien no puede borrar no tiene por que ver lo borrado.
    /// </summary>
    public bool IncluirEliminados { get; init; }

    /// <summary>Base 1, como lo cuenta la gente. La consulta lo traduce con <c>Saltar</c>.</summary>
    public int Numero { get; init; } = 1;

    public int Tamano { get; init; } = TamanoPorDefecto;

    /// <summary>
    /// Nombre de columna a ordenar. Cada Servicio lo traduce contra una LISTA BLANCA de
    /// columnas permitidas y cae en su orden por defecto si no reconoce el valor.
    /// Interpolar esto en SQL seria una inyeccion; con EF Core no se puede interpolar en
    /// un <c>OrderBy</c>, y esa limitacion aqui es una ventaja.
    /// </summary>
    public string? Orden { get; init; }

    public bool Descendente { get; init; }

    /// <summary>
    /// El tamano YA ACOTADO. Los Servicios usan esto, nunca <c>Tamano</c> crudo: el valor
    /// llega de la cadena de consulta y puede ser cero, negativo o absurdo.
    /// </summary>
    public int TamanoEfectivo => Math.Clamp(Tamano, 1, TamanoMaximo);

    /// <summary>Cuantas filas saltar. Es el <c>Skip</c> de la consulta.</summary>
    public int Saltar => (Math.Max(Numero, 1) - 1) * TamanoEfectivo;
}
