using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Equipos;
using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Archivos;
using Maquinaria.Infraestructura.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Equipos;

internal sealed class ServicioDocumentosEquipoEf(ContextoEmpresa bd)
    : IServicioDocumentosEquipo
{
    public async Task<IReadOnlyList<DocumentoEquipoDto>> ListarAsync(
        Guid equipoId, CancellationToken ct)
        => await bd.EquipoArchivos
            .AsNoTracking()
            .Where(a => a.EquipoId == equipoId && a.Archivo!.EliminadoEn == null)
            .OrderBy(a => a.Tipo)
            .ThenByDescending(a => a.CreadoEn)
            .Select(a => Proyectar(a))
            .ToListAsync(ct);

    private static DocumentoEquipoDto Proyectar(EquipoArchivo a) => new(
        a.Id,
        a.EquipoId,
        a.ArchivoId,
        a.Tipo,
        a.Descripcion,
        a.Archivo!.NombreOriginal,
        a.Archivo.TipoMime,
        a.Archivo.TamanoBytes,
        a.CreadoEn);

    public async Task<Resultado<DocumentoEquipoDto>> RegistrarAsync(
        Guid equipoId,
        ArchivoGuardado guardado,
        AltaDocumentoEquipo alta,
        Guid? subidoPorId,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(alta.Tipo))
        {
            return Resultado<DocumentoEquipoDto>.Invalido("El tipo de documento no es valido.");
        }

        if (!await bd.Equipos.AnyAsync(e => e.Id == equipoId && e.EliminadoEn == null, ct))
        {
            return Resultado<DocumentoEquipoDto>.NoEncontrado("El equipo no existe.");
        }

        var archivo = new Archivo
        {
            Ruta = guardado.Ruta,
            NombreOriginal = guardado.NombreOriginal,
            TipoMime = guardado.TipoMime,
            TamanoBytes = guardado.TamanoBytes,
            HashSha256 = guardado.HashSha256,
            SubidoPorId = subidoPorId,
        };

        var documento = new EquipoArchivo
        {
            EquipoId = equipoId,
            ArchivoId = archivo.Id,
            Tipo = alta.Tipo,
            Descripcion = string.IsNullOrWhiteSpace(alta.Descripcion)
                ? null
                : alta.Descripcion.Trim(),
        };

        // LAS DOS FILAS EN UN SOLO SaveChanges: EF las manda en la misma transaccion, asi que
        // no existe el estado «hay archivo y no hay documento».
        bd.Archivos.Add(archivo);
        bd.EquipoArchivos.Add(documento);

        await bd.SaveChangesAsync(ct);

        return Resultado<DocumentoEquipoDto>.Ok(new DocumentoEquipoDto(
            documento.Id,
            equipoId,
            archivo.Id,
            documento.Tipo,
            documento.Descripcion,
            archivo.NombreOriginal,
            archivo.TipoMime,
            archivo.TamanoBytes,
            documento.CreadoEn));
    }

    public Task<RutaDeDocumento?> ObtenerRutaAsync(
        Guid equipoId, Guid documentoId, CancellationToken ct)
        => bd.EquipoArchivos
            .AsNoTracking()
            .Where(a => a.Id == documentoId
                     && a.EquipoId == equipoId
                     && a.Archivo!.EliminadoEn == null)
            .Select(a => new RutaDeDocumento(
                a.Archivo!.Ruta, a.Archivo.NombreOriginal, a.Archivo.TipoMime))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<string>> BorrarAsync(
        Guid equipoId, Guid documentoId, CancellationToken ct)
    {
        var documento = await bd.EquipoArchivos
            .Include(a => a.Archivo)
            .FirstOrDefaultAsync(a => a.Id == documentoId && a.EquipoId == equipoId, ct);

        if (documento?.Archivo is null || documento.Archivo.EliminadoEn is not null)
        {
            return Resultado<string>.NoEncontrado("El documento no existe.");
        }

        var ruta = documento.Archivo.Ruta;

        // La fila de equipo_archivo si se borra —es el enlace, no un hecho historico— y la de
        // archivo se marca eliminada: su tamano sigue contando para el consumo de la empresa.
        bd.EquipoArchivos.Remove(documento);
        documento.Archivo.EliminadoEn = DateTime.UtcNow;

        await bd.SaveChangesAsync(ct);

        return Resultado<string>.Ok(ruta);
    }
}
