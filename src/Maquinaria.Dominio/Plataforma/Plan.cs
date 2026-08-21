namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Un plan comercial del producto. Vive en la base central.
///
/// Un plan retirado se marca <see cref="Activo"/> = false, nunca se borra: hay
/// suscripciones historicas que lo referencian.
/// </summary>
public class Plan
{
    /// <summary>
    /// uuid v7 generado en C#, no en Postgres: por eso no hace falta ninguna
    /// extension de UUID. Se asigna en el inicializador y no en el caso de uso
    /// porque en la Fase 5 la PWA de campo tendra que crear registros sin red.
    /// Al leer de la base, EF Core sobreescribe este valor.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Identificador estable del plan. UNIQUE.</summary>
    public required string Codigo { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>
    /// decimal, nunca double ni float: se mapea a numeric(18,4). Un float no
    /// representa 0.1 exactamente y el error se acumula en cada calculo de tarifa.
    /// </summary>
    public decimal PrecioMensual { get; set; }

    /// <summary>Codigo ISO 4217 de tres letras.</summary>
    public string Moneda { get; set; } = "MXN";

    /// <summary>Posicion en la que se muestra el plan al comparar.</summary>
    public int Orden { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }

    /// <summary>
    /// Solo get: nadie puede reemplazar la coleccion completa desde fuera, solo
    /// agregar y quitar. EF Core la puebla igual.
    /// </summary>
    public ICollection<PlanLimite> Limites { get; } = [];
}
