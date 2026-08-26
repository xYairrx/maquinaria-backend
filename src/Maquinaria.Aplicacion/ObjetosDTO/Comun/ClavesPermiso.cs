using Maquinaria.Dominio.Plataforma;
using Maquinaria.Dominio.Seguridad;

namespace Maquinaria.Aplicacion.Comun;

/// <summary>
/// Todas las claves de permiso que el sistema reconoce: el producto de los modulos por las
/// seis acciones.
///
/// EXISTE PARA QUE LA API NO TENGA QUE MIRAR AL DOMINIO. El bucle que registra una policy
/// por permiso necesita esta lista, y la regla de capas dice que <c>Api</c> habla con
/// <c>Aplicacion</c>, no con <c>Dominio</c>. Aqui se compone una vez y se expone ya armada.
///
/// La composicion es <c>modulo.accion</c> —<c>equipos.editar</c>— y las dos mitades siguen
/// viviendo donde vivian: <see cref="ClavesModulo"/> y <see cref="AccionesPermiso"/>. Esto
/// no las duplica, las multiplica.
/// </summary>
public static class ClavesPermiso
{
    public static readonly string[] Todas =
    [
        .. from modulo in ClavesModulo.Todas
           from accion in AccionesPermiso.Todas
           select $"{modulo}.{accion}"
    ];

    private static readonly HashSet<string> Conjunto = new(Todas, StringComparer.Ordinal);

    /// <summary>
    /// Si la clave es una de las reconocidas. Lo usa la prueba que comprueba que ningun
    /// <c>[RequierePermiso]</c> exige un permiso que no existe — un endpoint que pide un
    /// permiso inexistente es un endpoint inalcanzable.
    /// </summary>
    public static bool Existe(string clave) => Conjunto.Contains(clave);
}
