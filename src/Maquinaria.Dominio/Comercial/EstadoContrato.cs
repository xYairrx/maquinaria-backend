namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// En que punto esta un contrato.
///
/// EL VALOR 1 ES ESPECIAL: mientras es borrador se puede editar, y en cuanto pasa a
/// cualquier otro estado la base lo vuelve inmutable con un disparador. Lo pediste tu:
/// "una vez el contrato es autorizado ya no se puede editar".
/// </summary>
public enum EstadoContrato : short
{
    /// <summary>El unico estado editable.</summary>
    Borrador = 1,

    /// <summary>Autorizado internamente. Ya no se toca.</summary>
    Autorizado = 2,

    /// <summary>Firmado por el cliente.</summary>
    Firmado = 3,

    Terminado = 4,
}
