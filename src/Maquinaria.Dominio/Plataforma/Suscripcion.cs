namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// El contrato de una empresa con un plan: quien contrato que y en que periodo.
/// Es el cruce entre <see cref="Tenant"/> y <see cref="Plan"/>.
///
/// Una empresa no puede tener dos suscripciones vigentes traslapadas, y eso lo
/// garantiza un constraint EXCLUDE USING gist en PostgreSQL, no codigo de C#:
/// con un "if (existe) throw", dos peticiones simultaneas leerian ambas "no
/// existe" y ambas insertarian.
///
/// Este es el mismo mecanismo que en la Fase 1 impedira rentar dos veces el
/// mismo equipo en fechas traslapadas, pero sobre algo facil de razonar.
/// </summary>
public class Suscripcion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }

    public Guid PlanId { get; set; }

    /// <summary>
    /// Inicio y Fin son DOS columnas, no un NpgsqlRange.
    ///
    /// El constraint de no-traslape necesita un tstzrange, pero ese tipo solo se
    /// mapea en C# con NpgsqlRange&lt;T&gt;, que viene de Npgsql, y Maquinaria.Dominio
    /// no depende de infraestructura. Como los constraints EXCLUDE aceptan
    /// expresiones, en la migracion se escribe tstzrange(inicio, fin) y el
    /// dominio queda limpio. Mismo criterio para ocupacion_equipo en la Fase 1.
    /// </summary>
    public DateTime Inicio { get; set; }

    /// <summary>Null significa contrato indefinido.</summary>
    public DateTime? Fin { get; set; }

    public EstadoSuscripcion Estado { get; set; }

    public DateTime CreadoEn { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>
    /// Sin borrado en cascada desde Plan: un plan retirado se marca inactivo y
    /// nunca se borra, precisamente porque estas filas historicas lo referencian.
    /// </summary>
    public Plan? Plan { get; set; }
}
