namespace Maquinaria.Aplicacion.Comun;

/// <summary>
/// Una pagina de resultados, con lo que hace falta para pintar el paginador.
///
/// <c>Total</c> es el conteo COMPLETO de filas que cumplen el filtro, no las de esta
/// pagina: sin el, la pantalla no puede decir "51-100 de 3,842" ni dibujar el ultimo
/// boton. Cuesta una segunda consulta con <c>COUNT</c>, y se paga a proposito.
///
/// La propiedad se llama <c>Numero</c> y no <c>Pagina</c> porque un miembro no puede
/// llamarse igual que el tipo que lo contiene.
/// </summary>
public sealed record Pagina<T>(IReadOnlyList<T> Filas, int Numero, int Tamano, long Total)
{
    /// <summary>Cuantas paginas hay en total. Cero cuando no hay filas.</summary>
    public int Paginas => Tamano <= 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamano);

    /// <summary>
    /// Una pagina sin filas, que NO es lo mismo que un 404: el filtro es valido y no
    /// encontro nada. Un listado vacio se contesta con 200 y esto dentro.
    /// </summary>
    public static Pagina<T> Vacia(int numero, int tamano) => new([], numero, tamano, 0);
}
