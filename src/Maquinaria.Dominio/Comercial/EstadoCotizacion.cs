namespace Maquinaria.Dominio.Comercial;

/// <summary>Por donde va una cotizacion.</summary>
public enum EstadoCotizacion : short
{
    Borrador = 1,

    Enviada = 2,

    /// <summary>El cliente la esta revisando o pidio cambios.</summary>
    EnRevision = 3,

    /// <summary>Aceptada. Es la que puede convertirse en renta.</summary>
    Aceptada = 4,

    Rechazada = 5,

    /// <summary>Paso su vigencia sin respuesta.</summary>
    Vencida = 6,

    Cancelada = 7,
}
