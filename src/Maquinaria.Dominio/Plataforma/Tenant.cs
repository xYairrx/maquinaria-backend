namespace Maquinaria.Dominio.Plataforma;

/// <summary>
/// Una empresa suscrita. Vive en la base CENTRAL; sus datos de negocio (usuarios,
/// equipos, clientes, rentas) viven en su propia base de datos, cuyo nombre esta
/// en <see cref="NombreBd"/>.
///
/// Es la entidad mas sensible del sistema: <see cref="NombreBd"/> no es un dato
/// descriptivo, es el valor que decide a que base de datos se conecta la peticion.
/// </summary>
public class Tenant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Lo que la persona escribe en el campo "Empresa" al iniciar sesion. Es el
    /// identificador publico y estable: cambiarlo rompe el acceso de todos sus
    /// usuarios. UNIQUE, y su indice es el mas consultado del sistema porque cada
    /// login pasa por el.
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Nombre de su base de datos: maquinaria_&lt;slug con _ en lugar de -&gt;. Se usan
    /// guiones bajos porque un nombre de base con guiones obliga a entrecomillar
    /// en cada sentencia.
    ///
    /// Lleva un CHECK de formato en la base que es CONTROL DE SEGURIDAD, no
    /// cosmetica: los identificadores SQL no se pueden parametrizar, asi que el
    /// CREATE DATABASE se arma concatenando. La validacion debe repetirse con
    /// regex en C# antes de concatenar; no basta la de la base.
    /// </summary>
    public required string NombreBd { get; set; }

    public required string RazonSocial { get; set; }

    public string? NombreComercial { get; set; }

    public string? Rfc { get; set; }

    public string? Telefono { get; set; }

    public string? CorreoContacto { get; set; }

    /// <summary>Situacion comercial. Un tenant suspendido o cancelado no puede operar.</summary>
    public EstadoTenant Estado { get; set; } = EstadoTenant.Prueba;

    /// <summary>
    /// Si la invitacion del primer administrador SALIO por correo.
    ///
    /// EXISTE PORQUE EL PANEL NO PODIA SABERLO. El alta contestaba si el correo habia salido
    /// y ese dato moria en la respuesta HTTP: al recargar la pantalla, una empresa con la
    /// invitacion sin enviar era indistinguible de una con la invitacion entregada, y no
    /// habia desde donde reenviarla. Paso de verdad con una empresa real y un Resend en
    /// sandbox que rechazo el envio con un 403.
    ///
    /// FALSO significa «no salio, O NO SE SABE». Las empresas creadas antes de esta columna
    /// quedan en falso porque de verdad no se sabe, y eso es lo correcto: el panel ofrece
    /// reenviar, que es inofensivo —el reenvio rechaza si el administrador ya activo su
    /// cuenta— y es la unica salida si no salio.
    ///
    /// No es historial: solo dice como quedo el ultimo intento. Quien mando que y cuando es
    /// trabajo de la bitacora de auditoria, no de esta columna.
    /// </summary>
    public bool InvitacionEnviada { get; set; }

    /// <summary>
    /// SIN VALOR INICIAL A PROPOSITO. No es un olvido: C# no lo permite.
    ///
    /// Escribir "= EstadoAprovisionamiento.Pendiente" aqui NO compila, porque al
    /// resolver ese nombre dentro de la clase el compilador encuentra primero esta
    /// propiedad, que oculta al tipo homonimo, y entonces .Pendiente no existe.
    /// (Es lo que el analizador CA1721 de .NET desaconseja.)
    ///
    /// Encaja con el diseno: el aprovisionamiento tiene que ponerla en Pendiente
    /// como paso 1 de su secuencia, y el CHECK de la base atrapa a quien no lo haga.
    /// </summary>
    public EstadoAprovisionamiento EstadoAprovisionamiento { get; set; }

    /// <summary>
    /// Ultima migracion aplicada en la base de esta empresa. Sin esto, un fallo
    /// parcial al migrar N bases deja versiones desalineadas de forma invisible,
    /// y el desfase no se descubre hasta que algo truena.
    /// </summary>
    public string? VersionEsquema { get; set; }

    /// <summary>
    /// Zona horaria de PRESENTACION. Todo se guarda en UTC; esta define como se
    /// muestra, y el frontend tiene que respetarla en lugar de la del navegador.
    /// </summary>
    public string ZonaHoraria { get; set; } = "America/Mexico_City";

    /// <summary>Codigo ISO 4217 de tres letras.</summary>
    public string Moneda { get; set; } = "MXN";

    /// <summary>Dia del mes en que se le cobra, de 1 a 31. Null si no aplica.</summary>
    public short? DiaPago { get; set; }

    public DateTime CreadoEn { get; set; }

    public DateTime? ActualizadoEn { get; set; }

    /// <summary>
    /// Borrado logico con marca de tiempo, no un bool: un bool dice QUE se borro,
    /// esto dice CUANDO, que es lo que necesita la auditoria. Y habilita los
    /// indices parciales WHERE eliminado_en IS NULL.
    /// </summary>
    public DateTime? EliminadoEn { get; set; }

    public ICollection<Suscripcion> Suscripciones { get; } = [];

    /// <summary>
    /// Cupos de ESTA empresa. Coleccion DISPERSA: solo trae los limites que se le
    /// fijaron como excepcion. Vacia es lo normal y significa que hereda
    /// <see cref="TipoLimite.ValorDefecto"/> en todos, que arranca en ilimitado.
    ///
    /// No confundir con los modulos: esos los define el plan que contrato, via
    /// <see cref="Suscripcion"/> y <see cref="PlanModulo"/>.
    /// </summary>
    public ICollection<TenantLimite> Limites { get; } = [];
}
