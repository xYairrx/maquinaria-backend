using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Api.Comandos;

/// <summary>
/// El comando `migrar-empresas`: aplica las migraciones pendientes de ContextoEmpresa a
/// todas las bases de empresa y reporta empresa por empresa.
///
///     dotnet run --project src/Maquinaria.Api -- migrar-empresas
///
/// UN ARGUMENTO DE LINEA DE COMANDOS Y NO UN PROYECTO DE CONSOLA NUEVO. El comando
/// necesita exactamente la misma configuracion que la API —las dos cadenas de conexion,
/// que viven en los user-secrets de este proyecto— y el mismo contenedor de DI. Un
/// proyecto aparte serian otro csproj, otro juego de secretos y dos registros de
/// infraestructura que pueden divergir; a cambio de nada.
///
/// Corre y termina: se ejecuta antes de configurar el pipeline y NO abre ningun puerto.
///
/// Codigos de salida, que es lo que mira un script de despliegue:
///
///     0  todas al dia
///     1  al menos una fallo  (las demas SI se migraron)
///     2  no se pudo ni empezar: la base central no responde
/// </summary>
internal static class ComandoMigrarEmpresas
{
    public const string Nombre = "migrar-empresas";

    public static bool EstaSolicitado(string[] args)
        => args.Any(a => string.Equals(a, Nombre, StringComparison.OrdinalIgnoreCase));

    public static async Task<int> EjecutarAsync(IServiceProvider servicios)
    {
        // El caso de uso es scoped porque abajo tiene un ContextoCentral. Sin ambito, el
        // contenedor lo rechaza.
        using var ambito = servicios.CreateScope();
        var caso = ambito.ServiceProvider.GetRequiredService<MigrarEmpresas>();

        Console.WriteLine($"{Nombre}: aplicando las migraciones de ContextoEmpresa.");

        ReporteMigracion reporte;

        try
        {
            // Sin token cancelable: interrumpir a media corrida dejaria una base a medio
            // migrar en el peor momento, y una migracion no tarda tanto como para necesitar
            // Ctrl+C. Lo que falte se aplica en la siguiente pasada.
            reporte = await caso.EjecutarAsync(CancellationToken.None);
        }
        catch (Exception e)
        {
            // Aqui solo cae lo que impide EMPEZAR —tipicamente que la central no responde—.
            // Los fallos de una empresa concreta nunca llegan hasta aca: los atrapa el caso
            // de uso para no detener a las demas.
            Console.Error.WriteLine($"No se pudo iniciar la migracion: {e.Message}");

            return 2;
        }

        Imprimir(reporte);

        return reporte.HayFallas ? 1 : 0;
    }

    private static void Imprimir(ReporteMigracion reporte)
    {
        Console.WriteLine($"Version disponible en el codigo: {Version(reporte.VersionDisponible)}");
        Console.WriteLine();

        if (reporte.Empresas.Count == 0)
        {
            Console.WriteLine("No hay empresas registradas.");
            return;
        }

        var ancho = reporte.Empresas.Max(e => e.Slug.Length);

        foreach (var empresa in reporte.Empresas)
        {
            Console.WriteLine(
                $"{empresa.Slug.PadRight(ancho)}  {Etiqueta(empresa)}"
                + $"  {Version(empresa.VersionAntes)} -> {Version(empresa.VersionDespues)}");

            if (empresa.Motivo is not null)
            {
                Console.WriteLine($"{new string(' ', ancho)}  {empresa.Motivo}");
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"{reporte.Empresas.Count} empresas: {reporte.Migradas} migradas, "
            + $"{reporte.Omitidas} omitidas, {reporte.Fallidas} con fallo.");

        if (reporte.HayFallas)
        {
            // Repetido al final a proposito: con veinte empresas, la linea del fallo queda
            // fuera de la pantalla y el reporte tiene que dejar claro quien quedo atras.
            Console.WriteLine();
            Console.WriteLine("QUEDARON ATRAS: " + string.Join(
                ", ",
                reporte.Empresas
                    .Where(e => e.Estado == EstadoMigracion.Fallida)
                    .Select(e => e.Slug)));
        }
    }

    private static string Etiqueta(ResultadoMigracionEmpresa empresa) => empresa.Estado switch
    {
        EstadoMigracion.Migrada =>
            string.Equals(empresa.VersionAntes, empresa.VersionDespues, StringComparison.Ordinal)
                ? "OK (sin cambios)"
                : "OK (migrada)    ",
        EstadoMigracion.Omitida => "OMITIDA         ",
        _ => "FALLO           ",
    };

    private static string Version(string? version)
        => string.IsNullOrEmpty(version) ? "(ninguna)" : version;
}
