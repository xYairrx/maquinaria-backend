using System.Net;

namespace Maquinaria.Dominio.Trazabilidad;

/// <summary>
/// El rastro de cambios. Una fila por hecho, escrita por un interceptor de
/// SaveChanges o por el caso de uso, y NUNCA modificada ni borrada.
///
/// LA MISMA ENTIDAD SE USA EN LOS DOS CONTEXTOS. Vive en la base de cada empresa y
/// tambien en la central: dar de alta un tenant, suspenderlo, cambiarle el plan o
/// moverle un limite ocurre solo alla, y son las decisiones mas privilegiadas del
/// sistema. Como no tiene ni una relacion con nada, no hay razon para duplicar la
/// clase; cada contexto la configura en su propio espacio de nombres.
///
/// Es material de auditoria de primera linea que rol_permiso y usuario_rol tengan
/// llave compuesta: por eso <see cref="EntidadId"/> es texto y no uuid. Con una
/// columna uuid, las dos tablas que registran quien le dio que poder a quien
/// serian inauditables.
/// </summary>
public class Auditoria
{
    /// <summary>
    /// bigint identity, rompiendo la convencion de uuid v7 del proyecto a
    /// proposito: es la unica tabla de altisimo volumen a la que nunca apunta una
    /// FK, asi que los 8 bytes extra por fila del uuid, multiplicados por millones
    /// y por el indice, no compran nada.
    ///
    /// Se configura como GENERATED ALWAYS y no BY DEFAULT: la aplicacion NO PUEDE
    /// suministrar un id. En una bitacora append-only eso importa — nadie puede
    /// insertar en una posicion arbitraria de la secuencia ni pisar un numero.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Agrupa todo lo que se hizo en UNA operacion. Crear una renta escribe renta,
    /// renta_linea y ocupacion_equipo; sin esto son tres filas sueltas.
    ///
    /// SU ALCANCE ES LA OPERACION, NO EL SaveChanges. Si fuera por guardado
    /// fallarian los dos casos que importan: una peticion puede guardar mas de una
    /// vez —el aprovisionamiento lo hace— y las acciones 4 a 8 no pasan por
    /// SaveChanges, asi que no tendrian grupo.
    ///
    /// Se genera SIEMPRE del lado del servidor, aunque el cliente mande un
    /// X-Correlation-Id: un id que viene del cliente es un id que el cliente puede
    /// repetir para atribuir sus filas al grupo de otra persona.
    ///
    /// El mismo valor va en el log estructurado. Es lo que permite cruzar una
    /// excepcion tecnica con las filas de auditoria de esa operacion, y por eso no
    /// hace falta una columna de nivel aqui.
    /// </summary>
    public Guid CorrelacionId { get; set; }

    /// <summary>
    /// El reloj es el de la BASE, con DEFAULT now(), no el del servidor de
    /// aplicacion: con varias instancias sus relojes derivan y el orden del
    /// registro dejaria de ser confiable justo cuando se necesita.
    ///
    /// now() es el inicio de la TRANSACCION, asi que todas las filas de un mismo
    /// SaveChanges comparten instante. Es correcto —es la misma accion— y es la
    /// otra razon de <see cref="CorrelacionId"/>: el timestamp ordena, no agrupa.
    /// </summary>
    public DateTime FechaUtc { get; set; }

    /// <summary>
    /// SIN FK, y no solo por el costo de verificacion en la tabla mas escrita:
    /// puede apuntar legitimamente a una fila que NO EXISTE en esta base —un
    /// superadministrador vive en la central— asi que una FK no seria cara, seria
    /// INCORRECTA.
    ///
    /// NULL significa que no lo hizo un usuario de esta base. Cual de los dos casos
    /// es, lo dice <see cref="Origen"/>.
    /// </summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>
    /// El dato humano CONGELADO al momento de escribir.
    ///
    /// Los usuarios no se borran, asi que su justificacion no es "sobrevive al
    /// borrado" sino otras dos: el correo puede cambiar, y este campo registra bajo
    /// que identidad se actuo entonces; y para Origen = plataforma el UsuarioId
    /// JAMAS va a resolver dentro de esta base.
    ///
    /// El correo y no el nombre porque es unico y es la identidad de login; los
    /// nombres se repiten.
    /// </summary>
    public string? UsuarioCorreo { get; set; }

    /// <summary>
    /// Los CODIGOS de los roles efectivos en ese instante.
    ///
    /// Obligatorio desde que 'administrador' salta la verificacion por acceso_total:
    /// "'administrador' = ANY(roles)" responde si la accion paso por el bypass o por
    /// un permiso concedido. No se puede reconstruir despues, porque los roles y
    /// rol_permiso cambian.
    ///
    /// Los codigos y no los ids: la bitacora debe leerse sin joins a tablas que
    /// pudieron cambiar. string[] se mapea nativo a text[] en Npgsql, asi que
    /// Maquinaria.Dominio no gana dependencias.
    ///
    /// Arreglo VACIO para las acciones del sistema, nunca null: vacio afirma
    /// "ningun rol", null seria "no se sabe".
    /// </summary>
    public string[] Roles { get; set; } = [];

    /// <summary>
    /// Una de las constantes de <see cref="OrigenesAuditoria"/>. Es lo que
    /// desambigua un <see cref="UsuarioId"/> nulo. No admite null: "no sabemos de
    /// donde vino" no es una entrada aceptable en una bitacora.
    /// </summary>
    public required string Origen { get; set; }

    /// <summary>
    /// IPAddress y no string: se mapea a inet, que valida el formato, cubre IPv4 e
    /// IPv6 y permite preguntas de red. Npgsql no admite mapear string a inet.
    ///
    /// NULL porque un proceso en segundo plano no tiene IP.
    ///
    /// OJO en la politica de retencion: una IP es dato personal en varias
    /// jurisdicciones, y esta tabla nunca se borra.
    /// </summary>
    public IPAddress? Ip { get; set; }

    public AccionAuditoria Accion { get; set; }

    /// <summary>
    /// El nombre de la clase: 'Rol', 'RolPermiso', 'Usuario'. Texto y no un id a un
    /// catalogo de entidades, porque la bitacora tiene que leerse sin joins y
    /// porque con 75 entidades el catalogo seria una migracion por entidad.
    ///
    /// TRAMPA CONOCIDA: renombrar una clase parte la historia entre dos nombres.
    /// Paso el 2026-08-21 con UsuarioPlataforma -> Usuario, antes de que esta tabla
    /// existiera. Un rename es barato en codigo y caro en una bitacora: cuando
    /// vuelva a pasar, hay que dejarlo anotado.
    /// </summary>
    public required string Entidad { get; set; }

    /// <summary>
    /// TEXTO Y NO UUID, por tres casos que un uuid no cubre:
    ///
    /// - Llaves compuestas. rol_permiso y usuario_rol son dos uuids, y se guardan
    ///   como "{rol}:{permiso}". Son justo las tablas de permisos.
    /// - Llaves que no son uuid. Las hay en Fase 1.
    /// - Eventos sin entidad. Un LoginFallido guarda el correo intentado.
    /// </summary>
    public required string EntidadId { get; set; }

    /// <summary>
    /// jsonb con SOLO LAS PROPIEDADES QUE CAMBIARON, no la entidad completa: por
    /// tamano, y porque un diff de 40 campos donde cambio uno es ilegible. El
    /// ChangeTracker de EF ya sabe cuales son.
    ///
    /// LA LISTA DE EXCLUSION NO ES OPCIONAL: usuario.hash_contrasena,
    /// token_acceso.hash_token y sesion_refresh.hash_token nunca entran aqui. Esas
    /// columnas guardan hashes precisamente para que leer la base no de material
    /// usable, y la auditoria lo desharia por la puerta de atras, en la tabla que
    /// nunca se borra.
    /// </summary>
    public string? ValoresAnteriores { get; set; }

    /// <summary>
    /// En un <see cref="AccionAuditoria.Alta"/> no hay subconjunto que enviar, asi
    /// que lleva la entidad entera menos las exclusiones.
    ///
    /// El patron de nulos dice la accion sin mirar Accion: anteriores null es Alta,
    /// nuevos null es Borrado, los dos presentes es Cambio, y los dos null es uno
    /// de los eventos 4 a 8.
    /// </summary>
    public string? ValoresNuevos { get; set; }
}
