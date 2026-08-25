namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Una clausula del catalogo, para armar contratos de renta.
///
/// Agregado el 2026-08-24 a peticion del negocio. El M6 del documento funcional lista
/// como "informacion" del contrato cosas como responsabilidades, combustible, danos y
/// penalizaciones — y esas NO SON CAMPOS, SON CLAUSULAS. Con este catalogo, el contrato
/// se queda delgado —partes, fechas, deposito, estado— y los terminos viven aqui.
///
/// El TEXTO de esta fila es la plantilla vigente. Lo que un contrato firmado conserva es
/// una COPIA, en ContratoClausula: si esta plantilla se corrige manana, los contratos ya
/// firmados no cambian. Ver la nota de congelado alla.
/// </summary>
public class Clausula
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Codigo { get; set; }

    /// <summary>Encabezado con el que aparece en el contrato impreso.</summary>
    public required string Titulo { get; set; }

    /// <summary>
    /// El cuerpo. Texto libre y no plantilla con variables: sustituir campos dentro de
    /// una clausula es un motor de plantillas, y eso no se justifica hasta que alguien
    /// lo pida con un caso concreto.
    /// </summary>
    public required string Texto { get; set; }

    /// <summary>Posicion sugerida al armar el contrato.</summary>
    public int Orden { get; set; }

    /// <summary>
    /// Si entra en todo contrato por omision. Las de responsabilidad por danos y las de
    /// penalizacion suelen serlo; las de combustible o traslado dependen del trato.
    ///
    /// Es una SUGERENCIA al armar el documento, no una regla que la base imponga: quien
    /// redacta puede quitarla en un contrato concreto, y esa decision queda en la copia.
    /// </summary>
    public bool Obligatoria { get; set; }

    /// <summary>
    /// Una clausula retirada se marca inactiva, nunca se borra: hay contratos que la
    /// referencian como origen de su copia.
    /// </summary>
    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }
}
