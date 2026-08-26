namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Lo que el migrador necesita saber de una empresa. Uso INTERNO del servidor.
///
/// A diferencia de <see cref="ResumenEmpresa"/>, este si lleva NombreBd: el migrador
/// tiene que abrir esa base. Nunca sale en una respuesta HTTP.
/// </summary>
public sealed record TenantParaMigrar(
    Guid Id,
    string Slug,
    string RazonSocial,
    string NombreBd,
    Dominio.Plataforma.EstadoTenant Estado,
    Dominio.Plataforma.EstadoAprovisionamiento Aprovisionamiento);

/// <summary>Que le paso a UNA empresa.</summary>
public enum DesenlaceMigracion : short
{
    /// <summary>Ya estaba en la ultima version. No se toco.</summary>
    AlDia = 1,

    /// <summary>Se le aplicaron migraciones.</summary>
    Migrada = 2,

    /// <summary>
    /// No se intento. Su base no esta lista —aprovisionamiento a medias— y migrarla
    /// daria errores confusos en lugar de un mensaje claro.
    /// </summary>
    Omitida = 3,

    /// <summary>Trono. El detalle va en el mensaje.</summary>
    Fallida = 4,
}

/// <param name="Version">La ultima migracion aplicada al terminar.</param>
public readonly record struct ResultadoEmpresa(
    string Slug,
    DesenlaceMigracion Desenlace,
    string? Version,
    string? Detalle);

/// <summary>
/// El reporte completo. Lo importante es que hay UNA LINEA POR EMPRESA: con veinte
/// bases, "fallo algo" no sirve de nada — hay que saber cual.
/// </summary>
public sealed record ReporteMigracion(IReadOnlyList<ResultadoEmpresa> Empresas)
{
    public int Total => Empresas.Count;

    public int AlDia => Empresas.Count(e => e.Desenlace == DesenlaceMigracion.AlDia);

    public int Migradas => Empresas.Count(e => e.Desenlace == DesenlaceMigracion.Migrada);

    public int Omitidas => Empresas.Count(e => e.Desenlace == DesenlaceMigracion.Omitida);

    public int Fallidas => Empresas.Count(e => e.Desenlace == DesenlaceMigracion.Fallida);

    /// <summary>
    /// Para el codigo de salida del comando: en un despliegue automatizado, que una de
    /// veinte bases quede atras tiene que romper la tuberia, no pasar desapercibido.
    /// </summary>
    public bool HuboFallas => Fallidas > 0;
}

/// <summary>
/// Como esta el esquema de una empresa frente al esperado.
///
/// TRES CAMPOS Y NO UN BOOLEANO "al dia", y esa es la parte que importa. Con un solo
/// booleano, una base POR DELANTE del binario desplegado —se publico una version vieja de
/// la API— se reporta igual que una atrasada, y son problemas distintos con arreglos
/// distintos. El caso peligroso quedaba escondido detras del booleano.
/// </summary>
/// <param name="MigracionesPendientes">
/// Cuantas le faltan para llegar a la version del binario. Solo significa algo
/// cuando <see cref="VersionReconocida"/> es <c>true</c>.
/// </param>
/// <param name="Desfasada">
/// Si le faltan migraciones. Es lo unico que la pantalla necesita para pintar la alerta, y
/// por eso se calcula en el servidor: la regla de que es estar atrasado vive en un solo
/// lado. Solo significa algo cuando <see cref="VersionReconocida"/> es <c>true</c>.
/// </param>
/// <param name="VersionReconocida">
/// En <c>false</c> significa que NO SE PUDO COMPARAR: version nula —la base no tiene
/// historial, o no se pudo leer— o una migracion que este binario no conoce, que es el caso
/// de una base por delante del codigo. Ahi <see cref="Desfasada"/> y
/// <see cref="MigracionesPendientes"/> no dicen nada util y no hay que leerlos.
/// </param>
public readonly record struct EstadoEsquema(
    Guid Id,
    string Slug,
    string RazonSocial,
    Dominio.Plataforma.EstadoTenant Estado,
    Dominio.Plataforma.EstadoAprovisionamiento Aprovisionamiento,
    string? VersionAplicada,
    int MigracionesPendientes,
    bool Desfasada,
    bool VersionReconocida);

/// <summary>
/// La foto completa del esquema: la version del BINARIO una vez, y una linea por empresa.
///
/// <para>
/// La version va AQUI Y NO EN CADA EMPRESA por dos razones. Es la misma para todas —es la
/// del binario que responde, no la de ninguna empresa— asi que repetirla N veces invita a
/// que alguien lea la de una fila y la trate como suya. Y sobre todo: leyendola de la
/// primera empresa, un sistema SIN empresas reportaba <c>null</c> aunque el ensamblado si
/// trajera migraciones. El reporte decia menos de lo que sabia, y justo en el estado en el
/// que empieza todo despliegue nuevo.
/// </para>
/// </summary>
/// <param name="VersionDisponible">
/// La migracion mas avanzada del codigo. Nula solo si el ensamblado no trae ninguna, que no
/// pasa en produccion pero no debe reventar el reporte.
/// </param>
public sealed record ReporteEsquemas(
    string? VersionDisponible,
    IReadOnlyList<EstadoEsquema> Empresas);

/// <summary>
/// Aplica las migraciones de ContextoEmpresa a las bases de todas las empresas.
///
/// EXISTE PORQUE CADA EMPRESA TIENE SU PROPIA BASE. Un despliegue que agrega una tabla
/// no termina cuando la migracion esta escrita: termina cuando las N bases la tienen.
/// Sin esto hay que aplicar a mano, base por base, y con veinte clientes eso es una
/// receta para el desfase silencioso.
///
/// DEBE SER RESISTENTE A FALLOS PARCIALES: si truena en la empresa 23, las 22 anteriores
/// ya migraron y las siguientes no. Por eso registra version_esquema por empresa y
/// CONTINUA en lugar de abortar.
/// </summary>
public interface IMigradorEmpresas
{
    /// <param name="slug">Null para todas. Con valor, solo esa.</param>
    Task<ReporteMigracion> MigrarAsync(string? slug, CancellationToken ct);

    /// <summary>
    /// Solo mira, no toca. Es lo que alimenta el endpoint de salud que reporta quien
    /// quedo atrasado — sin eso, el desfase es invisible hasta que algo truena.
    /// </summary>
    Task<ReporteEsquemas> RevisarAsync(CancellationToken ct);
}
