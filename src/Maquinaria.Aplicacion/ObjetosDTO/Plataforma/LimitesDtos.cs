namespace Maquinaria.Aplicacion.Plataforma;

/// <summary>
/// Un cupo de una empresa, ya resuelto: lo que de verdad aplica y de donde sale.
///
/// SE DEVUELVEN LOS CUATRO TIPOS SIEMPRE, tenga o no fila la empresa. La tabla
/// <c>tenant_limite</c> es dispersa —solo guarda excepciones— y una pantalla que
/// mostrara nada mas las filas existentes ensenaria una empresa recien dada de alta
/// como si no tuviera limites de ningun tipo, cuando lo que pasa es que hereda todos
/// los valores por defecto.
/// </summary>
/// <param name="Valor">
/// El EFECTIVO: la excepcion del tenant si existe, y si no el valor por defecto del
/// tipo. <c>-1</c> es sin limite; <c>0</c> es valido y significa que no puede crear
/// ninguno.
/// </param>
/// <param name="EsExcepcion">
/// Si hay fila en <c>tenant_limite</c>. Es lo que distingue "300 negociados con este
/// cliente" de "300 porque es lo que trae el catalogo", y sin el no se puede saber que
/// hace el boton de quitar.
/// </param>
public sealed record LimiteDeEmpresa(
    string Clave,
    string Nombre,
    string Descripcion,
    string Unidad,
    int Valor,
    int ValorDefecto,
    bool EsExcepcion,
    int Orden);

/// <summary>
/// Los tres desenlaces que le importan al panel, con el mismo criterio que
/// <see cref="ResultadoPlan"/>: un rechazo por dato mal capturado no es un fallo.
///
/// <see cref="Limites"/> trae la lista COMPLETA ya actualizada, no solo el cupo tocado,
/// para que la pantalla se repinte de una respuesta en lugar de pedir el listado otra
/// vez. Es lo mismo que hace <c>CambiarActivoAsync</c> con el plan.
/// </summary>
public readonly record struct ResultadoLimites(
    bool Correcto,
    bool EmpresaExiste,
    string? Motivo,
    IReadOnlyList<LimiteDeEmpresa>? Limites)
{
    public static ResultadoLimites Exito(IReadOnlyList<LimiteDeEmpresa> limites)
        => new(true, true, null, limites);

    public static ResultadoLimites Rechazado(string motivo) => new(false, true, motivo, null);

    /// <summary>La empresa no existe: es 404, no 400.</summary>
    public static ResultadoLimites SinEmpresa() => new(false, false, null, null);
}

/// <summary>
/// El cuerpo del PUT. Un objeto y no el entero suelto, por lo mismo que
/// <c>CambioDeActivo</c>: la peticion se lee sola en un log.
/// </summary>
public readonly record struct FijarLimite(int Valor);
