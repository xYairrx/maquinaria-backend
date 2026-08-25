namespace Maquinaria.Dominio.Activos;

/// <summary>
/// El CALENDARIO de una maquina. Cada fila es un periodo en el que no esta libre.
///
/// ES LA TABLA QUE CUMPLE TU REQUISITO: "debemos mantener el control de no rentar la
/// misma equipo en fechas iguales". Y lo cumple en la BASE, con una restriccion
/// EXCLUDE USING gist que rechaza cualquier fila cuyo periodo se traslape con otra
/// activa del mismo equipo.
///
/// POR QUE NO SE VALIDA EN LA APLICACION: dos usuarios que reservan la misma maquina en
/// el mismo instante pasan los dos la comprobacion "esta libre?" y los dos insertan. Es
/// una carrera, y no se arregla con mas codigo: se arregla dejando que Postgres sea el
/// arbitro. La aplicacion consulta antes para dar un mensaje amable, pero la garantia
/// esta aqui abajo.
///
/// SEPARADA DE renta_linea a proposito: una maquina tambien se ocupa por mantenimiento o
/// por un traslado, y esos periodos tienen que competir por el calendario en igualdad de
/// condiciones con las rentas. Si el traslape se controlara sobre las lineas de renta,
/// mandar una maquina al taller no impediria rentarla.
/// </summary>
public class OcupacionEquipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    public DateTime Inicio { get; set; }

    /// <summary>Nulo = abierto, sin fecha de liberacion todavia.</summary>
    public DateTime? Fin { get; set; }

    public MotivoOcupacion Motivo { get; set; }

    /// <summary>
    /// A que apunta esta ocupacion segun el motivo: la renta, la orden de trabajo.
    ///
    /// SIN FK, y es una decision consciente: el destino cambia con el motivo, y una FK
    /// no puede apuntar a tablas distintas segun el valor de otra columna. La alternativa
    /// —una columna por motivo— llenaria la tabla de nulos.
    /// </summary>
    public Guid? ReferenciaId { get; set; }

    public string? Nota { get; set; }

    /// <summary>
    /// Si sigue contando para el traslape.
    ///
    /// La restriccion EXCLUDE solo mira las activas. Cancelar una reserva pone esto en
    /// falso en lugar de borrar la fila: asi el periodo se libera y ademas queda el
    /// rastro de que existio.
    /// </summary>
    public bool Activo { get; set; } = true;

    public DateTime CreadoEn { get; set; }
}
