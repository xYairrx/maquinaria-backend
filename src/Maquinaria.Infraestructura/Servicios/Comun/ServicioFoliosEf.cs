using Maquinaria.Aplicacion.Comun;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Comun;

/// <summary>
/// Folios por lectura del maximo. Ver la limitacion de concurrencia en <see cref="IFolios"/>.
/// </summary>
internal sealed class ServicioFoliosEf(ContextoEmpresa bd) : IFolios
{
    private static string Prefijo(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.Cotizacion => "COT",
        TipoDocumento.Renta => "REN",
        TipoDocumento.Contrato => "CTR",
        TipoDocumento.OrdenCompra => "OC",
        TipoDocumento.OrdenVenta => "OV",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
    };

    public async Task<string> SiguienteAsync(TipoDocumento tipo, CancellationToken ct)
    {
        // El ano sale de UtcNow y no de la zona del tenant. Es una simplificacion conocida: en
        // el cambio de ano, un documento capturado a las 19:00 del 31 de diciembre en Mexico
        // llevaria el ano siguiente. Corregirlo exige la zona horaria de la empresa, que existe
        // en `tenant.zona_horaria` y todavia no se usa en ningun calculo.
        var anio = DateTime.UtcNow.Year;
        var prefijo = $"{Prefijo(tipo)}-{anio}-";

        // Se piden los folios del ano y se toma el maximo del consecutivo. Ordenar por texto
        // funciona porque el consecutivo va con ceros a la izquierda y ancho fijo.
        var ultimo = await Folios(tipo)
            .Where(f => f.StartsWith(prefijo))
            .OrderByDescending(f => f)
            .FirstOrDefaultAsync(ct);

        var consecutivo = 1;

        if (ultimo is not null
            && int.TryParse(ultimo[prefijo.Length..], out var previo))
        {
            consecutivo = previo + 1;
        }

        return $"{prefijo}{consecutivo:00000}";
    }

    /// <summary>
    /// Los folios ya usados del tipo. Se proyecta solo la columna: traer la entidad para leer
    /// una cadena seria traer el documento entero.
    /// </summary>
    private IQueryable<string> Folios(TipoDocumento tipo) => tipo switch
    {
        TipoDocumento.Cotizacion => bd.Cotizaciones.Select(c => c.Folio),
        TipoDocumento.Renta => bd.Rentas.Select(r => r.Folio),
        TipoDocumento.Contrato => bd.Contratos.Select(c => c.Folio),
        TipoDocumento.OrdenCompra => bd.OrdenesCompra.Select(o => o.Folio),
        TipoDocumento.OrdenVenta => bd.OrdenesVenta.Select(o => o.Folio),
        _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
    };
}
