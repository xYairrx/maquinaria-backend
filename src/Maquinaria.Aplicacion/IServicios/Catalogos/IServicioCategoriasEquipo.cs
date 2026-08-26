using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// El catalogo de categorias de equipo.
///
/// NO HAY BORRADO, HAY DESACTIVACION, y no es una preferencia: <c>categoria_equipo</c> **no
/// tiene <c>eliminado_en</c>** —solo <c>equipo</c>, <c>archivo</c> y <c>tenant</c> lo
/// tienen—, asi que un borrado logico no cabe en el modelo; y un borrado fisico lo impide la
/// llave foranea de <c>tipo_equipo</c> en cuanto la categoria se haya usado una vez.
///
/// Es el mismo patron que <c>Plan.Activo</c> en el catalogo comercial y por la misma razon:
/// lo que el negocio quiere no es borrar el registro, es dejar de ofrecerlo sin romper lo
/// que ya lo referencia.
/// </summary>
public interface IServicioCategoriasEquipo
{
    Task<Pagina<CategoriaEquipoDto>> ListarAsync(Filtro filtro, CancellationToken ct);

    /// <summary>
    /// Devuelve <c>null</c> y no un <see cref="Resultado{T}"/>: «no existe» es el unico
    /// desenlace posible que no es el feliz, y el controlador ya traduce el nulo a 404.
    /// Envolverlo costaria una capa para no decir nada mas.
    /// </summary>
    Task<CategoriaEquipoDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<CategoriaEquipoDto>> CrearAsync(
        AltaCategoriaEquipo alta, CancellationToken ct);

    Task<Resultado<CategoriaEquipoDto>> EditarAsync(
        Guid id, AltaCategoriaEquipo cambio, CancellationToken ct);

    /// <summary>Retira o reactiva la categoria. Ver la nota de la interfaz.</summary>
    Task<Resultado<CategoriaEquipoDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct);
}
