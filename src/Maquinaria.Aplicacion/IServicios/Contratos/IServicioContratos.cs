using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Comercial;

namespace Maquinaria.Aplicacion.Contratos;

/// <summary>
/// Contratos y sus clausulas.
///
/// **FUERA DE BORRADOR NO SE TOCA NADA**, ni el contrato ni sus clausulas, y lo impone un trigger
/// —<c>contrato_inmutable</c> y su gemelo sobre <c>contrato_clausula</c>—, no la disciplina de
/// quien escriba el siguiente caso de uso. Es un documento con firmas: si se pudiera cambiar el
/// texto despues, la firma no significaria nada.
///
/// **NO HAY CANCELACION, y es una adaptacion al esquema migrado**: <c>EstadoContrato</c> solo
/// tiene Borrador, Autorizado, Firmado y Terminado —el CHECK es <c>BETWEEN 1 AND 4</c>—. El
/// alcance describe cancelar un contrato autorizado y hacer uno nuevo; eso exige un valor
/// <c>Cancelado</c> que la base no acepta hoy. Queda anotado en el plan de la fase.
/// </summary>
public interface IServicioContratos
{
    Task<Pagina<ContratoDto>> ListarAsync(FiltroContratos filtro, CancellationToken ct);

    Task<ContratoDto?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>Por renta, que es como lo busca la pantalla de la renta.</summary>
    Task<ContratoDto?> PorRentaAsync(Guid rentaId, CancellationToken ct);

    /// <summary>
    /// Crea el contrato y **congela** las clausulas: copia titulo y texto del catalogo. Lo llama
    /// el Proceso, que tambien valida el estado de la renta.
    /// </summary>
    Task<Resultado<ContratoDto>> CrearAsync(AltaContrato alta, CancellationToken ct);

    Task<Resultado<ContratoClausulaDto>> AgregarClausulaAsync(
        Guid contratoId, AltaContratoClausula clausula, CancellationToken ct);

    Task<Resultado> QuitarClausulaAsync(
        Guid contratoId, Guid clausulaId, CancellationToken ct);

    /// <summary>
    /// Borrador → Autorizado → Firmado → Terminado. Autorizar es el punto sin retorno: de ahi
    /// en adelante el trigger bloquea toda edicion.
    /// </summary>
    Task<Resultado<ContratoDto>> CambiarEstadoAsync(
        Guid id, EstadoContrato estado, CancellationToken ct);
}
