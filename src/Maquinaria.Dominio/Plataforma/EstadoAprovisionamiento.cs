namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Avance de la creacion de la base de datos de una empresa.
///
/// Existe porque insertar la fila en tenant e invocar CREATE DATABASE NO pueden
/// ser atomicos: PostgreSQL no permite CREATE DATABASE dentro de una transaccion,
/// y EF Core envuelve en transaccion por defecto. Hay entonces una ventana en la
/// que la fila existe y la base no. Este campo hace ese hueco visible y
/// reintentable, en lugar de dejar un huerfano que haya que borrar a mano.
/// </summary>
public enum EstadoAprovisionamiento : short
{
    /// <summary>La fila existe en tenant; su base de datos todavia no.</summary>
    Pendiente = 1,

    /// <summary>
    /// Estado TRANSITORIO: se esta creando, migrando o sembrando su base.
    /// Un tenant que se queda aqui delata un aprovisionamiento que murio a la
    /// mitad y hay que reintentar. Por eso es "Creando" y no "Creado": si fuera
    /// terminal seria redundante con <see cref="Lista"/> y se perderia esa senal.
    /// </summary>
    Creando = 2,

    /// <summary>Base creada, migrada y sembrada. La empresa puede operar.</summary>
    Lista = 3,

    /// <summary>Fallo la secuencia. El registro queda reintentable.</summary>
    Fallida = 4,
}
