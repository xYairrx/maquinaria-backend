using Maquinaria.Dominio.Archivos;

namespace Maquinaria.Dominio.Activos;

/// <summary>
/// Une una maquina con un documento.
///
/// Tabla intermedia y no una columna en <c>archivo</c>: un mismo archivo puede colgar de
/// varias cosas, y meter un equipo_id en la tabla de archivos obligaria a agregar una
/// columna nueva cada vez que otra entidad quiera adjuntar algo.
/// </summary>
public class EquipoArchivo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    public Guid ArchivoId { get; set; }

    public Archivo? Archivo { get; set; }

    public TipoArchivoEquipo Tipo { get; set; }

    public string? Descripcion { get; set; }

    public DateTime CreadoEn { get; set; }
}
