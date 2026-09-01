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

/// <summary>
/// Un tipo de limite del catalogo, como lo ve el panel.
/// </summary>
/// <param name="Reconocida">
/// Si hay CODIGO detras de la clave. Es el dato que decide si esta fila sirve para algo:
/// un tipo cuya clave no esta en <c>ClavesLimite</c> se puede crear, editar y fijar por
/// empresa — y no va a acotar nada nunca. Va en el DTO para que la pantalla pueda decirlo,
/// en lugar de dejar que alguien lo descubra el dia que el cupo no se respete.
/// </param>
/// <param name="Excepciones">
/// Cuantas empresas tienen un cupo propio de este tipo. Es lo que convierte la lista en algo
/// con lo que se puede decidir: retirar un tipo que veinte empresas tienen negociado no es
/// lo mismo que retirar uno que no usa nadie. Mismo papel que <c>Suscripciones</c> en
/// <see cref="ResumenPlan"/>.
/// </param>
public sealed record ResumenTipoLimite(
    Guid Id,
    string Clave,
    string Nombre,
    string Descripcion,
    string Unidad,
    int ValorDefecto,
    int Orden,
    bool Activo,
    bool Reconocida,
    int Excepciones);

/// <summary>
/// Lo que hace falta para crear un tipo de limite.
/// </summary>
/// <param name="Clave">
/// Identificador estable, en minusculas con guiones bajos. NO SE PUEDE EDITAR despues, por
/// lo mismo que el codigo de un plan: es lo que el codigo busca para aplicar el limite, asi
/// que cambiarla desconectaria la fila de lo unico que la hace servir.
/// </param>
/// <param name="ValorDefecto">
/// Lo que aplica a toda empresa que no tenga excepcion. <c>-1</c> es sin limite, y es el
/// valor con el que conviene nacer: un tipo que nace en cero deja a TODAS las empresas sin
/// poder crear ninguno, de golpe y sin que nadie lo haya pedido empresa por empresa.
/// </param>
public readonly record struct AltaTipoLimite(
    string Clave,
    string Nombre,
    string? Descripcion,
    string Unidad,
    int ValorDefecto,
    int Orden);

/// <summary>
/// Los campos editables de un tipo. La clave NO esta, a proposito.
///
/// Es un reemplazo completo de lo editable y no un parche campo a campo: con seis campos
/// opcionales habria que distinguir "no lo mandaron" de "lo mandaron vacio", y el panel
/// manda el formulario entero de todas formas.
/// </summary>
public readonly record struct CambioTipoLimite(
    string Nombre,
    string? Descripcion,
    string Unidad,
    int ValorDefecto,
    int Orden,
    bool Activo);

/// <summary>Mismo criterio que <see cref="ResultadoPlan"/>: un rechazo por dato no es un fallo.</summary>
public readonly record struct ResultadoTipoLimite(
    bool Correcto,
    string? Motivo,
    ResumenTipoLimite? Tipo)
{
    public static ResultadoTipoLimite Exito(ResumenTipoLimite tipo) => new(true, null, tipo);

    public static ResultadoTipoLimite Rechazado(string motivo) => new(false, motivo, null);
}
