using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Una empresa con lo justo para migrarla: su base y la version que la central dice
/// que tiene aplicada.
///
/// LLEVA nombre_bd, a diferencia de <see cref="ResumenEmpresa"/>, porque el comando
/// migrar-empresas necesita a que base conectarse. Por eso este tipo NO se devuelve
/// nunca por HTTP: el endpoint de salud lo proyecta a
/// <see cref="EstadoEsquemaEmpresa"/>, que no lo lleva.
/// </summary>
public sealed record EmpresaConEsquema(
    Guid Id,
    string Slug,
    string RazonSocial,
    string NombreBd,
    EstadoTenant Estado,
    EstadoAprovisionamiento Aprovisionamiento,
    string? VersionEsquema);

/// <summary>
/// El resultado de comparar lo que una base tiene aplicado contra lo que el codigo trae.
/// </summary>
/// <param name="VersionDisponible">
/// La migracion mas avanzada que trae el codigo. Nula solo si el ensamblado no tuviera
/// ninguna, que no puede pasar en produccion pero se contempla para no reventar.
/// </param>
/// <param name="Desfasada">
/// Si le faltan migraciones. Es el unico campo que la pantalla necesita para pintar la
/// alerta, y por eso se calcula aqui y no en el frontend: la regla de que es estar
/// atrasado tiene que vivir en un solo lado.
/// </param>
/// <param name="VersionReconocida">
/// Si la version que dice tener aplicada existe en el codigo. En <c>false</c> significa
/// que NO SE PUEDE comparar —version_esquema nula, o una migracion que este binario no
/// conoce, tipico de una base por delante del codigo desplegado— y entonces
/// <see cref="Desfasada"/> no dice nada util. Es informacion distinta de "esta atrasada"
/// y se reporta aparte a proposito: mezclarlas dejaria un caso raro escondido detras de
/// un booleano.
/// </param>
public readonly record struct ComparacionEsquema(
    string? VersionAplicada,
    string? VersionDisponible,
    int MigracionesPendientes,
    bool Desfasada,
    bool VersionReconocida);

/// <summary>
/// Compara la version aplicada de una empresa contra la lista de migraciones del codigo.
///
/// PURA a proposito: no toca ninguna base ni construye ningun contexto. Es la unica
/// logica no trivial de todo el bloque de migraciones, y asi se prueba sin Neon.
/// </summary>
public static class ComparadorEsquema
{
    /// <summary>
    /// <paramref name="disponibles"/> es la lista de migraciones del ensamblado EN ORDEN
    /// —tal como la devuelve EF Core—, y ese orden es el que manda. No se reordena aqui:
    /// el orden de aplicacion es el del historial, no el de una comparacion de cadenas.
    /// </summary>
    public static ComparacionEsquema Comparar(
        string? versionAplicada, IReadOnlyList<string> disponibles)
    {
        var ultima = disponibles.Count > 0 ? disponibles[^1] : null;
        var aplicada = versionAplicada?.Trim();

        if (string.IsNullOrEmpty(aplicada))
        {
            // Sin version no hay nada aplicado que valga: o el alta no llego a migrar, o
            // la base se creo por fuera. Cuenta como desfasada y le faltan todas.
            return new ComparacionEsquema(null, ultima, disponibles.Count, disponibles.Count > 0, false);
        }

        var indice = IndiceDe(disponibles, aplicada);

        if (indice < 0)
        {
            // Una version que este binario no conoce. No se inventa un numero de
            // pendientes: lo unico honesto es decir que no se pudo comparar.
            return new ComparacionEsquema(aplicada, ultima, 0, false, false);
        }

        var pendientes = disponibles.Count - 1 - indice;

        return new ComparacionEsquema(aplicada, ultima, pendientes, pendientes > 0, true);
    }

    private static int IndiceDe(IReadOnlyList<string> disponibles, string version)
    {
        for (var i = 0; i < disponibles.Count; i++)
        {
            // Ordinal: un id de migracion es un identificador, no texto de usuario.
            if (string.Equals(disponibles[i], version, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// Como ve el panel de superadministracion el esquema de una empresa.
///
/// NO LLEVA nombre_bd, por la misma razon que <see cref="ResumenEmpresa"/>: el panel no
/// necesita el nombre de la base de un cliente para nada.
/// </summary>
public sealed record EstadoEsquemaEmpresa(
    Guid Id,
    string Slug,
    string RazonSocial,
    EstadoTenant Estado,
    EstadoAprovisionamiento Aprovisionamiento,
    string? VersionAplicada,
    int MigracionesPendientes,
    bool Desfasada,
    bool VersionReconocida);

/// <summary>
/// El reporte completo del endpoint de salud de esquemas.
/// </summary>
/// <param name="VersionDisponible">
/// La migracion mas avanzada del codigo. Va UNA VEZ y no repetida en cada empresa: es la
/// misma para todas, porque es la del binario que responde.
/// </param>
/// <param name="Desfasadas">
/// Cuantas empresas quedaron atras. Es el numero que la pantalla necesita para decidir si
/// muestra alerta, sin recorrer la lista.
/// </param>
public sealed record ReporteSaludEsquemas(
    string? VersionDisponible,
    int TotalEmpresas,
    int Desfasadas,
    IReadOnlyList<EstadoEsquemaEmpresa> Empresas);
