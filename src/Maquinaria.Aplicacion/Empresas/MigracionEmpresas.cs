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
    string NombreBd,
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

/// <summary>Como esta el esquema de una empresa frente al esperado.</summary>
public readonly record struct EstadoEsquema(
    string Slug,
    string? VersionAplicada,
    string VersionEsperada,
    bool AlDia,
    Dominio.Plataforma.EstadoAprovisionamiento Aprovisionamiento);

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
    Task<IReadOnlyList<EstadoEsquema>> RevisarAsync(CancellationToken ct);
}
