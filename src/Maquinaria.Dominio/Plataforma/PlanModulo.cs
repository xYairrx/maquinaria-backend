namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Que modulos incluye un plan. Es la definicion del plan.
///
/// SIN Id propio: la llave es (PlanId, ModuloId), igual que rol_permiso y
/// usuario_rol. Un uuid de sustitucion aqui no serviria para nada — nadie
/// referencia una fila de esta tabla — y permitiria duplicados si alguien
/// olvidara el indice unico.
///
/// Consecuencia de diseno: como el plan ES su conjunto de modulos, un cliente
/// que necesite un modulo extra necesita otro plan. Si ese caso se vuelve comun,
/// hara falta un tenant_modulo de excepcion, espejo de <see cref="TenantLimite"/>.
/// </summary>
public class PlanModulo
{
    public Guid PlanId { get; set; }

    public Guid ModuloId { get; set; }

    public Plan? Plan { get; set; }

    public Modulo? Modulo { get; set; }
}
