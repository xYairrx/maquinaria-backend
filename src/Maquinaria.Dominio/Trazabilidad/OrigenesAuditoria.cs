namespace Maquinaria.Dominio.Trazabilidad;

/// <summary>
/// El unico lugar donde se escriben los valores de <see cref="Auditoria.Origen"/>.
///
/// Es texto y no un enum smallint como los demas, por una diferencia real:
/// AccionAuditoria es un dominio cerrado sobre el que el codigo ramifica, mientras
/// que el origen crece con los canales de entrega —un webhook, el comando
/// migrar-empresas, un job— y nada en el dominio lo lee para decidir. Un enum
/// obligaria a una migracion por canal nuevo.
/// </summary>
public static class OrigenesAuditoria
{
    /// <summary>Peticion HTTP a la API.</summary>
    public const string Api = "api";

    /// <summary>La aplicacion de campo, incluida su sincronizacion sin red.</summary>
    public const string Pwa = "pwa";

    /// <summary>
    /// Un superadministrador nuestro actuando DENTRO de la base de una empresa.
    /// Su usuario_id no va a resolver aqui: vive en la central. Es el caso en el
    /// que usuario_correo es el unico dato legible.
    /// </summary>
    public const string Plataforma = "plataforma";

    /// <summary>Proceso automatico: un job, una migracion, un comando.</summary>
    public const string Sistema = "sistema";

    public static readonly string[] Todos = [Api, Pwa, Plataforma, Sistema];
}
