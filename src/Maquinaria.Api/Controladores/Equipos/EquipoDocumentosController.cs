using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Equipos;
using Maquinaria.Aplicacion.Procesos.Equipos;
using Maquinaria.Api.Errores;
using Maquinaria.Api.Comun;
using Maquinaria.Api.Seguridad;
using Maquinaria.Dominio.Activos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maquinaria.Api.Controladores.Equipos;

/// <summary>
/// El expediente documental de un equipo: fotos, factura, poliza, manual, certificados.
///
/// LA SUBIDA VA COMO `multipart/form-data`, no como JSON con base64: el base64 crece un tercio
/// y obliga a tener el archivo entero en memoria de los dos lados.
///
/// **DIVERGENCIA CONSCIENTE con `convenciones.md`**, que dice que los archivos nunca se sirven
/// a traves de la API sino con URLs firmadas de vigencia corta. Con el almacenamiento en disco
/// no hay nada que firmar, asi que la descarga se sirve aqui. Cuando exista
/// `AlmacenamientoS3`, este endpoint debe pasar a devolver un **302 a la URL firmada** en lugar
/// de transmitir el contenido — y ese cambio es compatible con el cliente, que ya sigue
/// redirecciones.
/// </summary>
[ApiController]
[Route("api/equipos/{equipoId:guid}/documentos")]
[Tags("Equipos")]
[Authorize(PoliticasAutorizacion.Empresa)]
public sealed class EquipoDocumentosController(
    IServicioDocumentosEquipo servicio,
    ProcesoSubirDocumentoEquipo subir,
    ProcesoDocumentoEquipo proceso) : ControllerBase
{
    [HttpGet]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("ListarDocumentosDeEquipo")]
    [ProducesResponseType<IReadOnlyList<DocumentoEquipoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarAsync(Guid equipoId, CancellationToken ct)
        => Ok(await servicio.ListarAsync(equipoId, ct));

    [HttpPost]
    [RequierePermiso("equipos.editar")]
    [EndpointName("SubirDocumentoDeEquipo")]
    [EndpointSummary("Sube un documento al expediente. multipart/form-data.")]
    [ProducesResponseType<DocumentoEquipoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubirAsync(
        Guid equipoId,
        IFormFile archivo,
        [FromForm] TipoArchivoEquipo tipo,
        [FromForm] string? descripcion,
        CancellationToken ct)
    {
        if (archivo.Length == 0)
        {
            return Problem(
                title: "Peticion rechazada",
                detail: "El archivo viene vacio.",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = CodigosProblema.ArchivoVacio,
                });
        }

        await using var contenido = archivo.OpenReadStream();

        var resultado = await subir.EjecutarAsync(
            equipoId,
            new SolicitudDeGuardado(
                contenido,
                archivo.FileName,
                // El navegador puede no mandarlo; el tipo generico es mejor que un nulo que
                // luego hay que comprobar en cada descarga.
                string.IsNullOrWhiteSpace(archivo.ContentType)
                    ? "application/octet-stream"
                    : archivo.ContentType,
                // El prefijo real lo pone el Proceso, con el id del equipo, y el del tenant lo
                // pone el almacenamiento. Aqui no se compone ninguna ruta a proposito.
                Prefijo: string.Empty),
            new AltaDocumentoEquipo(tipo, descripcion),
            SubidoPor(),
            ct);

        return this.AHttp(
            resultado, d => $"/api/equipos/{equipoId}/documentos/{d.Id}/contenido");
    }

    [HttpGet("{id:guid}/contenido")]
    [RequierePermiso("equipos.consultar")]
    [EndpointName("DescargarDocumentoDeEquipo")]
    [EndpointSummary("Devuelve el contenido. Con S3 pasara a ser un 302 a una URL firmada.")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DescargarAsync(
        Guid equipoId, Guid id, CancellationToken ct)
    {
        var resultado = await proceso.DescargarAsync(equipoId, id, ct);

        return resultado.Correcto
            ? File(resultado.Valor!.Contenido, resultado.Valor.TipoMime,
                   resultado.Valor.NombreOriginal)
            : this.AHttp(resultado);
    }

    [HttpDelete("{id:guid}")]
    [RequierePermiso("equipos.eliminar")]
    [EndpointName("BorrarDocumentoDeEquipo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BorrarAsync(Guid equipoId, Guid id, CancellationToken ct)
        => this.AHttp(await proceso.BorrarAsync(equipoId, id, ct));

    /// <summary>
    /// Quien sube, del token. Nulo no rompe nada —<c>archivo.subido_por_id</c> es nullable—
    /// pero no deberia pasar: el endpoint exige sesion de empresa.
    /// </summary>
    private Guid? SubidoPor()
        => Guid.TryParse(
            User.FindFirst(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub)?.Value,
            out var id)
            ? id
            : null;
}
