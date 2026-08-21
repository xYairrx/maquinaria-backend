namespace Maquinaria.Dominio.Configuracion;

/// <summary>
/// Como interpretar el valor de un <see cref="Parametro"/>, que se guarda como
/// texto.
///
/// Arranca en 1 por la misma convencion del resto de los enums del proyecto.
/// </summary>
public enum TipoParametro : short
{
    Texto = 1,
    Entero = 2,
    Decimal = 3,
    Booleano = 4,
    Fecha = 5,
    Json = 6,
}
