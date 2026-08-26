using Maquinaria.Aplicacion.Comercio;
using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Procesos.Comercio;

/// <summary>
/// Finaliza una orden de compra: **registra en el catalogo el equipo de cada linea** y marca la
/// orden Finalizada.
///
/// Es el punto donde entra maquinaria al parque, y por eso es todo o nada: media orden finalizada
/// dejaria tres maquinas dadas de alta, la cuarta no, y la orden en un estado que no dice cual
/// falta.
/// </summary>
public sealed class ProcesoFinalizarOrdenCompra(
    IServicioOrdenesCompra ordenes,
    IUnidadDeTrabajo unidad)
{
    public async Task<Resultado<OrdenCompraDto>> EjecutarAsync(
        Guid ordenId, IReadOnlyList<RegistroDeEquipo> registros, CancellationToken ct)
    {
        var orden = await ordenes.ObtenerAsync(ordenId, ct);

        if (orden is null)
        {
            return Resultado<OrdenCompraDto>.NoEncontrado("La orden no existe.");
        }

        if (orden.Estado != Dominio.Comercial.EstadoOrden.Autorizada)
        {
            return Resultado<OrdenCompraDto>.Conflicto(
                $"La orden {orden.Folio} esta {orden.Estado}: solo se finaliza una Autorizada.");
        }

        // TODAS LAS LINEAS TIENEN QUE TRAER SU REGISTRO. Finalizar con la mitad dejaria lineas
        // sin equipo que nada volveria a mirar: la orden ya estaria Finalizada y no se puede
        // reabrir.
        var pendientes = orden.Detalles
            .Where(d => d.EquipoId is null)
            .Select(d => d.Id)
            .ToHashSet();

        var traidos = registros.Select(r => r.DetalleId).ToHashSet();

        if (!pendientes.SetEquals(traidos))
        {
            return Resultado<OrdenCompraDto>.Invalido(
                $"Cada linea sin equipo necesita su registro: faltan "
                + $"{pendientes.Except(traidos).Count()} y sobran "
                + $"{traidos.Except(pendientes).Count()}.");
        }

        // Los codigos internos no se pueden repetir entre si: el UNIQUE los rechazaria en el
        // segundo, ya con el primero creado dentro de la transaccion.
        var codigos = registros
            .Select(r => r.CodigoInterno?.Trim().ToUpperInvariant())
            .ToList();

        if (codigos.Distinct().Count() != codigos.Count)
        {
            return Resultado<OrdenCompraDto>.Invalido(
                "Hay codigos internos repetidos entre las lineas.");
        }

        await using var transaccion = await unidad.IniciarAsync(ct);

        foreach (var registro in registros)
        {
            var equipo = await ordenes.RegistrarEquipoAsync(ordenId, registro, ct);

            if (!equipo.Correcto)
            {
                return new Resultado<OrdenCompraDto>(
                    false, null, equipo.Razon, equipo.Motivo);
            }
        }

        var finalizada = await ordenes.MarcarFinalizadaAsync(ordenId, ct);

        if (!finalizada.Correcto)
        {
            return finalizada;
        }

        await transaccion.ConfirmarAsync(ct);

        return Resultado<OrdenCompraDto>.Ok((await ordenes.ObtenerAsync(ordenId, ct))!);
    }
}
