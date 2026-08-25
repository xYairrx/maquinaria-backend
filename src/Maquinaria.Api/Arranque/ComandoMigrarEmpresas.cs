using Maquinaria.Aplicacion.Empresas;

namespace Maquinaria.Api.Arranque;

/// <summary>
/// El comando migrar-empresas.
///
///     dotnet run -- migrar-empresas
///     dotnet run -- migrar-empresas --slug=bajio
///
/// Se ejecuta con el MISMO contenedor de servicios que la aplicacion, asi que usa la
/// misma resolucion de conexiones y la misma cadena directa. No hay un segundo camino de
/// codigo que pueda divergir del que corre en produccion.
/// </summary>
internal static class ComandoMigrarEmpresas
{
    public const string Nombre = "migrar-empresas";

    /// <returns>0 si todo bien; 1 si alguna empresa fallo.</returns>
    public static async Task<int> EjecutarAsync(WebApplication app, string[] args)
    {
        var slug = args
            .FirstOrDefault(a => a.StartsWith("--slug=", StringComparison.Ordinal))
            ?.Split('=', 2)[1];

        using var ambito = app.Services.CreateScope();
        var migrador = ambito.ServiceProvider.GetRequiredService<IMigradorEmpresas>();

        ReporteMigracion reporte;

        try
        {
            reporte = await migrador.MigrarAsync(slug, CancellationToken.None);
        }
        catch (Exception e)
        {
            // Esto atrapa lo que pasa ANTES del bucle por empresa: sobre todo no poder
            // leer la lista de empresas de la base central. El try de dentro del migrador
            // solo cubre el fallo de UNA empresa.
            //
            // Un comando de mantenimiento tiene que decir que paso, no volcar una pila de
            // cincuenta lineas: quien lo corre en un despliegue necesita leerlo de un
            // golpe.
            Console.Error.WriteLine();
            Console.Error.WriteLine("  NO SE PUDO MIGRAR: " + e.Message);
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "  Casi siempre es la cadena de conexion. Comprueba");
            Console.Error.WriteLine(
                "  ConnectionStrings:Migraciones en los secretos: tiene que ser la");
            Console.Error.WriteLine(
                "  cadena DIRECTA, sin -pooler, porque esto ejecuta DDL.");
            Console.Error.WriteLine();

            // El mensaje de arriba ya se imprimio; loguear la excepcion COMPLETA aqui
            // volcaria las cincuenta lineas de pila justo debajo y desharia el trabajo.
            // En un comando de consola la consola ES el log, asi que la pila se pide
            // aparte con --detalle.
            if (args.Contains("--detalle", StringComparer.Ordinal))
            {
                Console.Error.WriteLine(e);
                Console.Error.WriteLine();
            }
            else
            {
                Console.Error.WriteLine("  Corre otra vez con --detalle para ver la pila.");
                Console.Error.WriteLine();
            }

            app.Logger.LogError(
                "Fallo migrar-empresas antes de recorrer las empresas: {Mensaje}", e.Message);

            return 2;
        }

        // El reporte va a la consola linea por empresa. Con veinte bases, "fallo algo" no
        // sirve: hay que poder ver cual y por que sin abrir el log estructurado.
        Console.WriteLine();
        Console.WriteLine($"  {"EMPRESA",-24}  {"RESULTADO",-10}  DETALLE");
        Console.WriteLine($"  {new string('-', 24)}  {new string('-', 10)}  {new string('-', 40)}");

        foreach (var e in reporte.Empresas)
        {
            var detalle = e.Detalle ?? e.Version ?? string.Empty;

            if (detalle.Length > 60)
            {
                detalle = detalle[..60] + "...";
            }

            Console.WriteLine($"  {e.Slug,-24}  {Etiqueta(e.Desenlace),-10}  {detalle}");
        }

        Console.WriteLine();
        Console.WriteLine(
            $"  {reporte.Total} empresas: {reporte.AlDia} al dia, {reporte.Migradas} migradas, "
            + $"{reporte.Omitidas} omitidas, {reporte.Fallidas} fallidas.");
        Console.WriteLine();

        if (reporte.HuboFallas)
        {
            Console.WriteLine("  HAY EMPRESAS SIN MIGRAR. Revisa el log y reintenta.");
            Console.WriteLine();
        }

        // Codigo de salida distinto de cero: en un despliegue automatizado, que una de
        // veinte bases quede atras tiene que romper la tuberia y no pasar desapercibido.
        return reporte.HuboFallas ? 1 : 0;
    }

    private static string Etiqueta(DesenlaceMigracion d) => d switch
    {
        DesenlaceMigracion.AlDia => "al dia",
        DesenlaceMigracion.Migrada => "MIGRADA",
        DesenlaceMigracion.Omitida => "omitida",
        DesenlaceMigracion.Fallida => "FALLIDA",
        _ => d.ToString(),
    };
}
