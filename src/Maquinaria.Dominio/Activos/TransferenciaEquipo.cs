using Maquinaria.Dominio.Organizacion;

namespace Maquinaria.Dominio.Activos;

/// <summary>
/// Un traspaso de maquina entre bodegas y patios. Lo pediste asi: "se deben permitir
/// traspasos de equipos entre bodegas y patios".
///
/// ES UN HISTORICO, no un estado. La ubicacion actual vive en <see cref="Equipo"/>; esta
/// tabla guarda cada movimiento, quien lo hizo y por que. Sin ella, mover una maquina
/// perderia el rastro anterior y nadie podria responder "donde estaba en marzo".
///
/// Origen y destino solo pueden ser sitios que resguarden equipo. Lo garantiza un
/// disparador, no un CHECK: la regla depende del tipo de OTRA fila.
/// </summary>
public class TransferenciaEquipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    public Guid OrigenId { get; set; }

    public Ubicacion? Origen { get; set; }

    public Guid DestinoId { get; set; }

    public Ubicacion? Destino { get; set; }

    /// <summary>Quien lo movio. Siempre hay un responsable.</summary>
    public Guid TrabajadorId { get; set; }

    public Trabajador? Trabajador { get; set; }

    public DateTime Fecha { get; set; }

    public string? Motivo { get; set; }

    public DateTime CreadoEn { get; set; }
}
