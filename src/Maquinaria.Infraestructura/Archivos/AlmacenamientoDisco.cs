using System.Security.Cryptography;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Empresas;
using Microsoft.Extensions.Options;

namespace Maquinaria.Infraestructura.Archivos;

/// <summary>
/// Almacenamiento en disco, para desarrollo. El hermano de produccion es S3 sobre Cloudflare
/// R2 y todavia no existe.
///
/// EL PREFIJO DEL TENANT LO PONE ESTA CLASE, no quien llama. Es la unica forma de garantizar
/// que ninguna ruta se escape de su empresa: el llamador pide
/// <c>equipos/{id}</c> y aqui se convierte en <c>{tenantId}/equipos/{id}/{archivo}</c>. Si el
/// prefijo llegara del cuerpo de la peticion, un <c>../</c> bien puesto leeria los archivos de
/// otro cliente.
/// </summary>
internal sealed class AlmacenamientoDisco(
    IOptions<OpcionesAlmacenamiento> opciones,
    IContextoTenant tenant) : IAlmacenamientoArchivos
{
    private OpcionesAlmacenamiento Opciones => opciones.Value;

    public async Task<ArchivoGuardado> GuardarAsync(
        SolicitudDeGuardado solicitud, CancellationToken ct)
    {
        // Nombre generado, no el del usuario: el original se guarda en la tabla y se devuelve
        // al descargar. Usarlo como nombre de archivo traeria acentos, espacios, mayusculas
        // que en un bucket son otro objeto, y la posibilidad de sobrescribir lo que ya estaba.
        var extension = Path.GetExtension(solicitud.NombreOriginal);
        var nombre = $"{Guid.CreateVersion7()}{extension}";

        var relativa = CombinarRuta(solicitud.Prefijo, nombre);
        var absoluta = Absoluta(relativa);

        Directory.CreateDirectory(Path.GetDirectoryName(absoluta)!);

        // El hash se calcula MIENTRAS se escribe, no releyendo el archivo: son 25 MB como
        // maximo, pero releerlos duplica la E/S por nada.
        using var sha = SHA256.Create();
        long total = 0;

        await using (var destino = File.Create(absoluta))
        {
            var buffer = new byte[81920];
            int leidos;

            while ((leidos = await solicitud.Contenido.ReadAsync(buffer, ct)) > 0)
            {
                total += leidos;

                if (total > Opciones.MaximoBytes)
                {
                    // Se corta y se limpia: dejar el archivo a medias ocuparia espacio que
                    // nada volveria a mirar, porque la fila de `archivo` no se creo.
                    destino.Close();
                    File.Delete(absoluta);

                    throw new ArchivoDemasiadoGrande(Opciones.MaximoBytes);
                }

                sha.TransformBlock(buffer, 0, leidos, null, 0);
                await destino.WriteAsync(buffer.AsMemory(0, leidos), ct);
            }
        }

        sha.TransformFinalBlock([], 0, 0);

        return new ArchivoGuardado(
            relativa,
            solicitud.NombreOriginal,
            solicitud.TipoMime,
            total,
            Convert.ToHexString(sha.Hash!).ToLowerInvariant());
    }

    public Task<Stream?> AbrirAsync(string ruta, CancellationToken ct)
    {
        var absoluta = Absoluta(Validar(ruta));

        return Task.FromResult<Stream?>(
            File.Exists(absoluta) ? File.OpenRead(absoluta) : null);
    }

    public Task<bool> EliminarAsync(string ruta, CancellationToken ct)
    {
        var absoluta = Absoluta(Validar(ruta));

        if (!File.Exists(absoluta))
        {
            return Task.FromResult(false);
        }

        File.Delete(absoluta);

        return Task.FromResult(true);
    }

    /// <summary>
    /// Compone la ruta relativa con el prefijo del tenant al frente.
    /// </summary>
    private string CombinarRuta(string prefijo, string nombre)
        => $"{tenant.Actual.Id}/{prefijo.Trim('/')}/{nombre}";

    /// <summary>
    /// LA RUTA NO PUEDE SALIRSE DE SU TENANT NI DE LA RAIZ. Se comprueba dos veces porque son
    /// dos ataques distintos: el segmento <c>..</c> y una ruta absoluta.
    ///
    /// Y ademas se exige que empiece por el id del tenant actual: sin eso, un id de archivo de
    /// otra empresa —que vive en otra base, pero cuya ruta es adivinable— serviria para leer
    /// su documento.
    /// </summary>
    private string Validar(string ruta)
    {
        var limpia = ruta.Replace('\\', '/').TrimStart('/');

        if (limpia.Split('/').Contains("..") || Path.IsPathRooted(ruta))
        {
            throw new UnauthorizedAccessException("Ruta de archivo no valida.");
        }

        if (!limpia.StartsWith($"{tenant.Actual.Id}/", StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException(
                "La ruta no pertenece a la empresa de la peticion.");
        }

        return limpia;
    }

    private string Absoluta(string relativa)
        => Path.GetFullPath(Path.Combine(Opciones.Raiz, relativa));
}

/// <summary>
/// El archivo pasa del tope. Es excepcion y no <c>Resultado</c> porque se descubre a mitad de
/// la escritura, cuando el caso de uso ya devolvio el control al flujo de copia.
/// </summary>
internal sealed class ArchivoDemasiadoGrande(long maximo)
    : Exception($"El archivo pasa del maximo de {maximo / (1024 * 1024)} MB.")
{
    public long Maximo { get; } = maximo;
}
