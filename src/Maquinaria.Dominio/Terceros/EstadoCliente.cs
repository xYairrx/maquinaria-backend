namespace Maquinaria.Dominio.Terceros;

/// <summary>En que situacion esta un cliente.</summary>
public enum EstadoCliente : short
{
    Activo = 1,

    /// <summary>Suspendido por cartera, por siniestro, por lo que sea. Reversible.</summary>
    Suspendido = 2,

    /// <summary>Ya no se opera con el. Nunca se borra: sus rentas siguen ahi.</summary>
    Baja = 3,
}
