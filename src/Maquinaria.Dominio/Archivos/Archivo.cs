namespace Maquinaria.Dominio.Archivos;

/// <summary>
/// El indice de lo que vive en el almacenamiento de objetos. La tabla, no el
/// binario.
///
/// Existe desde la Fase 0 aunque las evidencias sean Fase 2, por cuatro razones:
///
/// - CUOTAS. R2 no ofrece un "peso total por prefijo" barato, asi que el consumo
///   del tenant es SUM(tamano_bytes) de esta tabla.
/// - HUERFANOS. Un archivo puede subirse y fallar el guardado del registro que lo
///   usa.
/// - DEDUPLICACION. <see cref="HashSha256"/> evita volver a subir un manual de
///   40 MB que ya esta.
/// - REFERENCIA UNICA. evidencia y equipo_documento apuntaran a Archivo.Id, no a
///   una ruta suelta.
/// </summary>
public class Archivo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Prefijada por el slug de la empresa:
    /// {slug}/equipos/{id}/inspecciones/{id}/{archivo_id}.jpg
    ///
    /// Aunque las bases esten separadas, el bucket es COMPARTIDO, y el prefijo hace
    /// trivial calcular consumo, aplicar cuotas y borrar todo al dar de baja un
    /// cliente. UNIQUE.
    /// </summary>
    public required string Ruta { get; set; }

    public required string NombreOriginal { get; set; }

    public required string TipoMime { get; set; }

    /// <summary>bigint: un video de campo pasa de los 2 GB que aguanta un int.</summary>
    public long TamanoBytes { get; set; }

    /// <summary>Para deduplicar. Nullable: no se calcula para todo.</summary>
    public string? HashSha256 { get; set; }

    public int? AnchoPx { get; set; }

    public int? AltoPx { get; set; }

    /// <summary>Nullable: lo pudo subir un proceso del sistema.</summary>
    public Guid? SubidoPorId { get; set; }

    public DateTime CreadoEn { get; set; }

    /// <summary>
    /// BAJA LOGICA, NUNCA FISICA: no hay DELETE en esta tabla.
    ///
    /// Aqui <see cref="EliminadoEn"/> sobrevive —a diferencia de usuario, que paso a
    /// un estado— porque marca algo que un estado no daria: el momento en que dejo
    /// de existir el BINARIO en el almacenamiento. La fila se queda para que el
    /// registro que lo referenciaba siga siendo legible.
    ///
    /// Habilita ademas el indice parcial WHERE eliminado_en IS NULL.
    /// </summary>
    public DateTime? EliminadoEn { get; set; }
}
