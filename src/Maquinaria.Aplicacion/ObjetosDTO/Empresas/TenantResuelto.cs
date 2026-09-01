using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Todo lo que hay que saber de una empresa para atender una peticion suya, resuelto
/// de una sola vez.
///
/// Se resuelve UNA VEZ —al iniciar sesion, o al primer toque de una peticion— y se
/// cachea. La alternativa, consultar la central en cada peticion, son dos consultas
/// extra por request contra una base que ni siquiera es la del cliente.
///
/// NombreBd no sale nunca de la capa de servidor: no viaja en el JWT ni en ninguna
/// respuesta. Un JWT va firmado pero NO cifrado, y los nombres de las bases de los
/// clientes no son informacion para el navegador.
/// </summary>
/// <param name="Modulos">
/// Claves de los modulos que incluye el plan contratado. Es la mitad izquierda de la
/// compuerta de autorizacion: el permiso efectivo es
/// "permisos del rol interseccion estos modulos".
/// </param>
/// <param name="Limites">
/// Cupos de la empresa por clave de tipo de limite. DISPERSO: solo trae las
/// excepciones que se le fijaron. Lo que no esta aqui usa el valor por defecto del
/// catalogo, que arranca en ilimitado.
/// </param>
public sealed record TenantResuelto(
    Guid Id,
    string Slug,
    string NombreBd,

    /// <summary>
    /// Para mostrar en pantalla: la pantalla de invitacion dice a que empresa se
    /// esta entrando. Viene de la misma fila que ya se leyo, asi que no cuesta nada.
    /// </summary>
    string RazonSocial,
    EstadoTenant Estado,
    EstadoAprovisionamiento Aprovisionamiento,
    string ZonaHoraria,
    string Moneda,
    IReadOnlySet<string> Modulos,
    IReadOnlyDictionary<string, int> Limites)
{
    /// <summary>
    /// Si la empresa puede operar. Un tenant suspendido o cancelado NO puede, y uno
    /// cuya base todavia no esta lista tampoco: abrir una base a medio aprovisionar
    /// daria errores de tabla inexistente en lugar de un mensaje claro.
    /// </summary>
    public bool PuedeOperar => BaseDisponible
        && Estado is EstadoTenant.Prueba or EstadoTenant.Activo;

    /// <summary>
    /// Si su base de datos existe y esta migrada. Es la MITAD TECNICA de
    /// <see cref="PuedeOperar"/>, separada porque hay un caso que necesita distinguirlas:
    /// el login de una empresa suspendida.
    ///
    /// Para poder decirle «tu servicio esta suspendido» a quien acierta su contrasena hay
    /// que poder comprobar esa contrasena, y eso exige abrir su base. Una empresa suspendida
    /// tiene base; una que todavia se esta aprovisionando, no —y abrirla daria errores de
    /// tabla inexistente en lugar de un mensaje claro—.
    ///
    /// NO SE USA PARA AUTORIZAR NADA. Quien decide si la empresa opera sigue siendo
    /// <see cref="PuedeOperar"/>.
    /// </summary>
    public bool BaseDisponible => Aprovisionamiento is EstadoAprovisionamiento.Lista;

    /// <summary>
    /// El cupo efectivo de un limite: el del tenant si lo declaro, o el que se pase
    /// como valor por defecto del catalogo.
    /// </summary>
    public int LimiteEfectivo(string clave, int valorDefecto)
        => Limites.TryGetValue(clave, out var valor) ? valor : valorDefecto;

    public bool IncluyeModulo(string claveModulo) => Modulos.Contains(claveModulo);
}
