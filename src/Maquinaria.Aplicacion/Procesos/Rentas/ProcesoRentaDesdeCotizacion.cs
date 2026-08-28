using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Cotizaciones;
using Maquinaria.Aplicacion.Rentas;
using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Procesos.Rentas;

/// <summary>
/// Convierte una cotizacion aceptada en una renta en Borrador.
///
/// **COPIA LOS PRECIOS CONGELADOS, no los vuelve a leer del catalogo.** Si entre la cotizacion y
/// la renta alguien cargo un precio nuevo, la renta tiene que cobrar lo que se cotizo: eso es lo
/// que hace que el numero que el cliente recuerda y el que el sistema factura sean el mismo.
///
/// LAS LINEAS SIN EQUIPO NO PASAN A <c>renta_linea</c>. Una cotizacion puede pedir «una
/// excavadora de 20 t» —tipo, sin equipo— y una renta necesita la maquina concreta, porque cada
/// linea genera una fila de calendario. Esas lineas se informan como pendientes y las asigna
/// quien captura; las que no tienen ni equipo ni tipo —el flete— pasan a <c>renta_concepto</c>,
/// que es donde les toca.
/// </summary>
public sealed class ProcesoRentaDesdeCotizacion(
    IServicioCotizaciones cotizaciones,
    IServicioRentas rentas,
    IUnidadDeTrabajo unidad)
{
    public async Task<Resultado<ConversionDeCotizacion>> EjecutarAsync(
        Guid cotizacionId, ConversionARenta datos, CancellationToken ct)
    {
        var cotizacion = await cotizaciones.ObtenerAsync(cotizacionId, ct);

        if (cotizacion is null)
        {
            return Resultado<ConversionDeCotizacion>.NoEncontrado("La cotizacion no existe.");
        }

        if (cotizacion.Estado != EstadoCotizacion.Aceptada)
        {
            return Resultado<ConversionDeCotizacion>.Conflicto(
                $"La cotizacion esta {cotizacion.Estado}: solo se convierte una Aceptada.");
        }

        await using var transaccion = await unidad.IniciarAsync(ct);

        var creada = await rentas.CrearAsync(
            new AltaRenta(
                cotizacion.ClienteId,
                cotizacionId,
                cotizacion.TrabajadorId,
                datos.Inicio,
                datos.Fin,
                datos.Lugar,
                datos.Deposito,
                datos.Anticipo,
                // El descuento y los impuestos se arrastran de la cotizacion: es lo que se
                // acordo. Se pueden corregir despues, con la renta en Borrador.
                cotizacion.Descuento,
                cotizacion.Impuestos,
                $"Desde cotizacion {cotizacion.Folio}"),
            ct);

        if (!creada.Correcto)
        {
            return new Resultado<ConversionDeCotizacion>(
                false, null, creada.Razon, creada.Motivo);
        }

        var renta = creada.Valor!;
        var pendientes = new List<string>();

        foreach (var linea in cotizacion.Lineas)
        {
            if (linea.EquipoId is Guid equipoId)
            {
                var agregada = await rentas.AgregarLineaAsync(
                    renta.Id,
                    new AltaRentaLinea(
                        equipoId,
                        linea.TarifaId,
                        linea.Cantidad,
                        // EL PRECIO COTIZADO, tal cual.
                        linea.PrecioUnitario,
                        HorasIncluidas: null,
                        linea.Orden),
                    ct);

                if (!agregada.Correcto)
                {
                    return new Resultado<ConversionDeCotizacion>(
                        false, null, agregada.Razon, agregada.Motivo);
                }

                continue;
            }

            if (linea.TipoEquipoId is not null)
            {
                // Cotizada por tipo: hay que elegir la maquina. Se informa en lugar de
                // adivinarla — asignar «cualquier excavadora libre» es una decision comercial,
                // no una conversion de documento.
                pendientes.Add(
                    $"{linea.Cantidad:0.##} x {linea.TipoEquipo} ({linea.Tarifa}) a "
                    + $"{linea.PrecioUnitario:N2}: falta asignar el equipo.");

                continue;
            }

            // Ni equipo ni tipo: es un cargo —flete, maniobras— y su lugar es renta_concepto.
            var concepto = await rentas.AgregarConceptoAsync(
                renta.Id,
                new AltaRentaConcepto(
                    linea.TarifaId,
                    TrabajadorId: null,
                    linea.Descripcion,
                    linea.Cantidad,
                    linea.PrecioUnitario,
                    Costo: null),
                ct);

            if (!concepto.Correcto)
            {
                return new Resultado<ConversionDeCotizacion>(
                    false, null, concepto.Razon, concepto.Motivo);
            }
        }

        await transaccion.ConfirmarAsync(ct);

        return Resultado<ConversionDeCotizacion>.Ok(new ConversionDeCotizacion(
            (await rentas.ObtenerAsync(renta.Id, ct))!, pendientes));
    }
}

/// <summary>
/// Lo que la conversion necesita y la cotizacion no tiene: el periodo real y el lugar de
/// trabajo. Una cotizacion no los lleva —cotiza un precio, no un compromiso de fechas—.
/// </summary>
public readonly record struct ConversionARenta(
    DateTime Inicio,
    DateTime Fin,
    LugarRenta Lugar,
    decimal Deposito,
    decimal Anticipo);

/// <param name="Pendientes">
/// Las lineas cotizadas por tipo de equipo que hay que asignar a una maquina concreta antes de
/// confirmar. Vacia si la cotizacion traia todos los equipos.
/// </param>
public sealed record ConversionDeCotizacion(
    RentaDto Renta,
    IReadOnlyList<string> Pendientes);
