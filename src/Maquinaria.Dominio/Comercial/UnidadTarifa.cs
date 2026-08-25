namespace Maquinaria.Dominio.Comercial;

/// <summary>
/// Como se cuantifica una <see cref="Tarifa"/>.
///
/// Es lo que dice si el precio se multiplica por horas, por dias, o se cobra una sola
/// vez. Sin esto, "tarifa de flete: 3500" es ambiguo — 3500 por que.
/// </summary>
public enum UnidadTarifa : short
{
    Hora = 1,
    Dia = 2,
    Semana = 3,
    Mes = 4,

    /// <summary>Se cobra una vez, sin multiplicar. Un flete, una maniobra.</summary>
    Evento = 5,

    Kilometro = 6,
}
