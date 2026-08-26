using Maquinaria.Aplicacion.Comun;

namespace Maquinaria.Aplicacion.Catalogos;

/// <summary>
/// El catalogo de clausulas.
///
/// EDITAR UNA CLAUSULA AQUI NO CAMBIA NINGUN CONTRATO YA GENERADO, y eso es la garantia
/// central del modulo: <c>contrato_clausula</c> COPIA titulo y texto al generar el contrato y
/// guarda <c>clausula_id</c> solo como referencia de donde salio. Corregir la plantilla afecta
/// a los contratos futuros, nunca a los firmados.
/// </summary>
public interface IServicioClausulas
{
    Task<Pagina<ClausulaDto>> ListarAsync(FiltroClausulas filtro, CancellationToken ct);

    Task<ClausulaDto?> ObtenerAsync(Guid id, CancellationToken ct);

    Task<Resultado<ClausulaDto>> CrearAsync(AltaClausula alta, CancellationToken ct);

    Task<Resultado<ClausulaDto>> EditarAsync(Guid id, AltaClausula cambio, CancellationToken ct);

    Task<Resultado<ClausulaDto>> CambiarActivoAsync(Guid id, bool activo, CancellationToken ct);
}
