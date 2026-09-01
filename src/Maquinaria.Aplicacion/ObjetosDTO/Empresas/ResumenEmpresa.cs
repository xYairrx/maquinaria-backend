using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Una empresa como la ve el panel de plataforma.
///
/// NO LLEVA nombre_bd. El panel no necesita saber el nombre de la base de un cliente
/// para nada, y todo dato que no sale del servidor es un dato que no se puede filtrar.
/// </summary>
/// <param name="Aprovisionamiento">
/// Lo que hace este listado util de verdad: un tenant en Fallida o atorado en Creando
/// se ve aqui, que es el punto de tener el campo.
/// </param>
public sealed record ResumenEmpresa(
    Guid Id,
    string Slug,
    string RazonSocial,
    string? Rfc,
    EstadoTenant Estado,
    EstadoAprovisionamiento Aprovisionamiento,
    string? VersionEsquema,
    string? CodigoPlan,
    int Modulos,

    /// <summary>
    /// Si la invitacion del primer administrador salio por correo. FALSO significa «no salio
    /// o no se sabe»: las empresas anteriores a esta columna quedan en falso porque de verdad
    /// no se sabe, y el panel ofrece reenviar, que es la unica salida si no salio.
    ///
    /// Va en la LISTA y no solo en la respuesta del alta porque ahi moria: al recargar el
    /// panel, una empresa sin invitacion entregada era indistinguible de una con ella, y no
    /// habia desde donde reenviarla.
    /// </summary>
    bool InvitacionEnviada,
    DateTime CreadoEn);

/// <summary>
/// El cuerpo del PATCH que mueve la situacion comercial de una empresa.
///
/// Un objeto y no el enum suelto, por lo mismo que <c>CambioDeActivo</c>: la peticion se
/// lee sola en un log y se le pueden agregar campos —un motivo, una fecha— sin cambiar la
/// firma. Y aqui eso no es hipotetico: suspender a un cliente es la clase de decision de
/// la que alguien va a querer saber el porque.
/// </summary>
public readonly record struct CambioDeEstadoEmpresa(EstadoTenant Estado);
