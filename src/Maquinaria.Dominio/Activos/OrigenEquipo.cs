namespace Maquinaria.Dominio.Activos;

/// <summary>Como entro la maquina al inventario.</summary>
public enum OrigenEquipo : short
{
    /// <summary>Llego por una orden de compra. La orden queda como su respaldo.</summary>
    Compra = 1,

    /// <summary>Carga inicial: ya era de la empresa antes de usar el sistema.</summary>
    CargaInicial = 2,
}
