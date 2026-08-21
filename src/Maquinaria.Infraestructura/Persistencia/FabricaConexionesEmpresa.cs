using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Maquinaria.Infraestructura.Persistencia;

/// <summary>
/// Arma cadenas de conexion para la base de una empresa.
///
/// EL UNICO LUGAR donde un nombre_bd se convierte en una cadena de conexion, y por
/// eso el unico lugar donde hace falta validarlo.
/// </summary>
public sealed partial class FabricaConexionesEmpresa
{
    /// <summary>
    /// El MISMO patron que el CHECK tenant_bd_formato de la base.
    ///
    /// La validacion se repite aqui a proposito, y no es redundancia: los
    /// identificadores SQL no se pueden parametrizar, asi que el CREATE DATABASE se
    /// arma CONCATENANDO. Confiar solo en el CHECK significa confiar en que nadie
    /// escriba nunca un nombre_bd por otra via —una carga masiva, un script, un
    /// panel de Neon—. Aqui se valida el dato justo antes de concatenarlo.
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9_]{2,62}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoNombreBd();

    private readonly string _cadenaPooled;
    private readonly string _cadenaDirecta;

    public FabricaConexionesEmpresa(
        IConfiguration configuracion, IOptions<OpcionesMultiTenancy> opciones)
    {
        _cadenaPooled = configuracion.GetConnectionString("Central")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Central.");

        // LA APLICACION NECESITA LA CADENA DIRECTA EN TIEMPO DE EJECUCION, no solo
        // para migrar. El aprovisionamiento ejecuta CREATE DATABASE, que es DDL, y el
        // endpoint pooled corre PgBouncer en modo transaccion y no lo admite. Por eso
        // Railway tiene que llevar las DOS cadenas configuradas.
        _cadenaDirecta = configuracion.GetConnectionString("Migraciones")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:Migraciones. La necesita el aprovisionamiento en "
                + "tiempo de ejecucion, no solo dotnet ef: CREATE DATABASE es DDL y el "
                + "endpoint pooled no lo admite.");

        Prefijo = opciones.Value.PrefijoBaseDatos;
    }

    public string Prefijo { get; }

    /// <summary>
    /// Deriva el nombre de base de un slug: guiones por guiones bajos.
    ///
    /// Se usan guiones bajos porque un nombre de base con guiones obliga a
    /// entrecomillar el identificador en cada sentencia.
    /// </summary>
    public string NombreBdDesdeSlug(string slug)
        => Prefijo + slug.Trim().ToLowerInvariant().Replace('-', '_');

    /// <summary>Cadena para atender peticiones: la POOLED.</summary>
    public string CadenaDeAplicacion(string nombreBd)
        => ConCambioDeBase(_cadenaPooled, nombreBd);

    /// <summary>
    /// Cadena para DDL y migraciones: la DIRECTA, sin PgBouncer.
    /// </summary>
    public string CadenaDeMigracion(string nombreBd)
        => ConCambioDeBase(_cadenaDirecta, nombreBd);

    /// <summary>
    /// Cadena directa contra la base CENTRAL, para ejecutar el CREATE DATABASE de una
    /// empresa nueva. No admite parametrizar el identificador, de ahi la validacion.
    /// </summary>
    public string CadenaCentralDirecta() => _cadenaDirecta;

    public static void ValidarNombreBd(string nombreBd)
    {
        if (!FormatoNombreBd().IsMatch(nombreBd))
        {
            // El mensaje NO incluye el valor recibido: si viniera de una entrada
            // hostil, repetirlo en un log o en una respuesta lo propaga.
            throw new ArgumentException(
                "El nombre de base de datos no cumple el formato permitido.", nameof(nombreBd));
        }
    }

    private static string ConCambioDeBase(string cadenaBase, string nombreBd)
    {
        ValidarNombreBd(nombreBd);

        // NpgsqlConnectionStringBuilder y no un reemplazo de texto: la cadena de Neon
        // trae SSL Mode y Channel Binding, y armarla a mano perderia alguno.
        return new NpgsqlConnectionStringBuilder(cadenaBase) { Database = nombreBd }
            .ConnectionString;
    }
}
