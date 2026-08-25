namespace Maquinaria.Dominio.Activos;

/// <summary>Por que una maquina esta comprometida en un periodo.</summary>
public enum MotivoOcupacion : short
{
    /// <summary>Rentada. La referencia apunta a la renta.</summary>
    Renta = 1,

    /// <summary>Reservada desde dentro. Solo usuarios internos, como lo pediste.</summary>
    Reserva = 2,

    Mantenimiento = 3,

    Reparacion = 4,

    Traslado = 5,

    /// <summary>Bloqueo manual: exhibicion, resguardo legal, lo que sea.</summary>
    Bloqueo = 6,
}
