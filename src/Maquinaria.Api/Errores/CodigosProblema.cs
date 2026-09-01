namespace Maquinaria.Api.Errores;

/// <summary>
/// Codigos de problema ESTABLES, para que el cliente pueda traducir el mensaje.
///
/// POR QUE HACEN FALTA. El `detail` de un `ProblemDetails` lo redacta el servidor y el
/// frontend lo muestra tal cual — es una regla suya, y buena: reescribir en el cliente los
/// mensajes de login podria deshacer sin querer la uniformidad que los hace no filtrar
/// clientes. El precio era que esos textos llegan SIEMPRE en espanol, porque la API no lee
/// `Accept-Language`, y en una interfaz en ingles se veian en espanol.
///
/// Un codigo rompe el empate: viaja en `extensions`, no es texto para leer, y el cliente lo
/// traduce a SU idioma sin inventarse nada. El `detail` sigue viajando y sigue siendo lo
/// que se muestra cuando el codigo no se reconoce, asi que un mensaje nuevo del servidor no
/// se queda mudo mientras nadie lo traduzca.
///
/// LO QUE UN CODIGO NO PUEDE HACER: distinguir mas de lo que el servidor decidio distinguir.
/// No hay un codigo por cada causa del rechazo uniforme del login —seria el enumerador de
/// clientes por la puerta de atras—: hay uno solo, el mismo para todas.
/// </summary>
internal static class CodigosProblema
{
    /// <summary>
    /// El rechazo uniforme del login: la empresa no existe, su base no esta lista, el correo
    /// no existe, el usuario no esta activo, o la contrasena no coincide. UNO para las cinco.
    /// </summary>
    public const string CredencialesIncorrectas = "credenciales_incorrectas";

    /// <summary>
    /// Las credenciales ERAN correctas y la empresa no puede operar. Solo se emite despues
    /// de verificarlas; ver <c>IniciarSesionEmpresa</c>.
    /// </summary>
    public const string ServicioSuspendido = "servicio_suspendido";

    public const string ServicioCancelado = "servicio_cancelado";

    /// <summary>
    /// Limite de peticiones. Lleva ademas <c>segundos</c> en `extensions`, para que el
    /// cliente pueda decir cuanto falta en su propio idioma en lugar de recibir la frase
    /// entera hecha.
    /// </summary>
    public const string DemasiadosIntentos = "demasiados_intentos";

    // ------------------------------------------------------------------
    // Los del resto de la API
    // ------------------------------------------------------------------

    /// <summary>
    /// La fila que se pidio no existe. Lleva ademas <c>entidad</c> en `extensions` —
    /// "marca", "renta", "cliente"— para que el cliente diga CUAL en su idioma, en vez de
    /// tener un codigo por cada una de las dieciseis.
    /// </summary>
    public const string NoEncontrado = "no_encontrado";

    public const string PeriodoObligatorio = "periodo_obligatorio";

    public const string PeriodoInvertido = "periodo_invertido";

    /// <summary>La liga de invitacion o de restablecimiento no sirve. Uniforme a proposito.</summary>
    public const string LigaNoValida = "liga_no_valida";

    public const string CredencialesObligatorias = "credenciales_obligatorias";

    public const string ArchivoVacio = "archivo_vacio";

    public const string AltaEmpresaIncompleta = "alta_empresa_incompleta";

    public const string EstadoNoValido = "estado_no_valido";
}
