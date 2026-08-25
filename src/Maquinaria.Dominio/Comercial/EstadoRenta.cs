namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// El ciclo de vida de una renta, de la firma a la devolucion.
///
/// Son diez y no tres porque el negocio distingue de verdad entre "confirmada pero sin
/// entregar" y "trabajando": son dos situaciones con acciones distintas y con gente
/// distinta pendiente de ellas.
/// </summary>
public enum EstadoRenta : short
{
    Borrador = 1,

    /// <summary>Cerrada con el cliente. Ya compromete el calendario del equipo.</summary>
    Confirmada = 2,

    /// <summary>Lista para salir, esperando el flete.</summary>
    PorEntregar = 3,

    EnTraslado = 4,

    /// <summary>El equipo esta en la obra, trabajando.</summary>
    Activa = 5,

    /// <summary>Le quedan pocos dias. Es el aviso para renovar o recoger.</summary>
    PorVencer = 6,

    /// <summary>Paso su fin y el equipo sigue fuera.</summary>
    Vencida = 7,

    /// <summary>El equipo ya volvio, falta cerrar cuentas.</summary>
    Devuelta = 8,

    /// <summary>Terminada y liquidada.</summary>
    Cerrada = 9,

    Cancelada = 10,
}
