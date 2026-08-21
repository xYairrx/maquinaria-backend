namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// El cupo de una empresa para un tipo de limite. Base central.
///
/// Los limites cuelgan del TENANT y no del plan: un cliente que negocia 300
/// equipos con un plan de 200 no obliga a inventarle un plan a medida que ensucie
/// el catalogo comercial. El plan define QUE MODULOS tiene (ver
/// <see cref="PlanModulo"/>); el tenant define CUANTO.
///
/// La tabla es DISPERSA a proposito: solo guarda excepciones. Un tenant sin filas
/// hereda <see cref="TipoLimite.ValorDefecto"/>, asi que la cadena de resolucion
/// tiene dos niveles y no tres:
///
///     tenant_limite.valor  →  tipo_limite.valor_defecto
///
/// Y una advertencia para cuando se escriba la verificacion: el limite vive en la
/// base CENTRAL y el consumo vive en la base de la EMPRESA (contar equipos,
/// contar usuarios, SUM(archivo.tamano_bytes)). No hay tabla de acumulados y no
/// hay transaccion que abarque las dos bases. Los limites del tenant se resuelven
/// una vez, junto con nombre_bd, no en cada peticion.
/// </summary>
public class TenantLimite
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid TipoLimiteId { get; set; }

    /// <summary>
    /// <see cref="TipoLimite.Ilimitado"/> o un entero mayor o igual a cero. Cero
    /// es valido y significa que la empresa no puede crear ninguno: es distinto
    /// de no tener fila, que significa "usa el valor por defecto".
    /// </summary>
    public int Valor { get; set; }

    public Tenant? Tenant { get; set; }

    public TipoLimite? TipoLimite { get; set; }
}
