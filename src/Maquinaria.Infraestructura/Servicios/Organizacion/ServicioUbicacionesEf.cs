using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Organizacion;
using Maquinaria.Dominio.Organizacion;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Organizacion;

internal sealed class ServicioUbicacionesEf(ContextoEmpresa bd) : IServicioUbicaciones
{
    /// <summary>Los dos tipos que almacenan. Se repite en varios predicados.</summary>
    private static readonly TipoUbicacion[] Almacenan =
        [TipoUbicacion.Bodega, TipoUbicacion.Patio];

    private static readonly TipoUbicacion[] Administran =
        [TipoUbicacion.Sucursal, TipoUbicacion.Patio];

    public async Task<Pagina<UbicacionDto>> ListarAsync(
        FiltroUbicaciones filtro, CancellationToken ct)
    {
        var consulta = bd.Ubicaciones.AsNoTracking();

        if (filtro.Tipo is TipoUbicacion tipo)
        {
            consulta = consulta.Where(u => u.Tipo == tipo);
        }

        // SE FILTRA POR TIPO Y NO POR LA PROPIEDAD CALCULADA: `AlmacenaEquipo` es una
        // propiedad de C# sin setter, asi que EF no la traduce a SQL. Las columnas generadas
        // existen en la base, pero el modelo no las mapea, y traducir el predicado a
        // `Tipo IN (1,3)` es exactamente lo que ellas contienen.
        if (filtro.AlmacenaEquipo is true)
        {
            consulta = consulta.Where(u => Almacenan.Contains(u.Tipo));
        }

        if (filtro.EsAdministrativa is true)
        {
            consulta = consulta.Where(u => Administran.Contains(u.Tipo));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(u =>
                EF.Functions.ILike(u.Nombre, $"%{texto}%")
                || EF.Functions.ILike(u.Codigo, $"%{texto}%"));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(u => u.Activo == activo);
        }

        var total = await consulta.LongCountAsync(ct);

        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("codigo", false) => consulta.OrderBy(u => u.Codigo),
            ("codigo", true) => consulta.OrderByDescending(u => u.Codigo),
            ("tipo", false) => consulta.OrderBy(u => u.Tipo).ThenBy(u => u.Nombre),
            ("tipo", true) => consulta.OrderByDescending(u => u.Tipo).ThenBy(u => u.Nombre),
            (_, true) => consulta.OrderByDescending(u => u.Nombre),
            _ => consulta.OrderBy(u => u.Nombre),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(u => new UbicacionDto(
                u.Id, u.Codigo, u.Nombre, u.Tipo, u.Domicilio, u.Telefono,
                u.Latitud, u.Longitud, u.Activo,
                bd.Equipos.Count(e => e.UbicacionId == u.Id && e.EliminadoEn == null)))
            .ToListAsync(ct);

        return new Pagina<UbicacionDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    public Task<UbicacionDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Ubicaciones
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UbicacionDto(
                u.Id, u.Codigo, u.Nombre, u.Tipo, u.Domicilio, u.Telefono,
                u.Latitud, u.Longitud, u.Activo,
                bd.Equipos.Count(e => e.UbicacionId == u.Id && e.EliminadoEn == null)))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<UbicacionDto>> CrearAsync(
        AltaUbicacion alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<UbicacionDto>.Invalido(invalido);
        }

        var codigo = alta.Codigo.Trim().ToUpperInvariant();

        if (await bd.Ubicaciones.AnyAsync(u => u.Codigo == codigo, ct))
        {
            return Resultado<UbicacionDto>.Conflicto(
                $"Ya existe una ubicacion con el codigo '{codigo}'.");
        }

        var ubicacion = new Ubicacion
        {
            Codigo = codigo,
            Nombre = alta.Nombre.Trim(),
            Tipo = alta.Tipo,
            Domicilio = Vacio(alta.Domicilio),
            Telefono = Vacio(alta.Telefono),
            Latitud = alta.Latitud,
            Longitud = alta.Longitud,
        };

        bd.Ubicaciones.Add(ubicacion);

        return await GuardarAsync(ubicacion, ct);
    }

    public async Task<Resultado<UbicacionDto>> EditarAsync(
        Guid id, AltaUbicacion cambio, CancellationToken ct)
    {
        if (Validar(cambio) is string invalido)
        {
            return Resultado<UbicacionDto>.Invalido(invalido);
        }

        var ubicacion = await bd.Ubicaciones.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (ubicacion is null)
        {
            return Resultado<UbicacionDto>.NoEncontrado("La ubicacion no existe.");
        }

        var codigo = cambio.Codigo.Trim().ToUpperInvariant();

        if (await bd.Ubicaciones.AnyAsync(u => u.Codigo == codigo && u.Id != id, ct))
        {
            return Resultado<UbicacionDto>.Conflicto(
                $"Ya existe otra ubicacion con el codigo '{codigo}'.");
        }

        // BAJAR UN PATIO A SUCURSAL LE QUITA LA CAPACIDAD DE ALMACENAR. Si ya tiene equipos,
        // el trigger equipo_exigir_almacen no los revisa —solo corre al insertar o mover un
        // equipo— asi que el cambio dejaria filas invalidas que nada volveria a mirar. Se
        // rechaza aqui, que es el unico lugar donde se puede.
        if (!Almacena(cambio.Tipo) && Almacena(ubicacion.Tipo))
        {
            var equipos = await bd.Equipos
                .CountAsync(e => e.UbicacionId == id && e.EliminadoEn == null, ct);

            if (equipos > 0)
            {
                return Resultado<UbicacionDto>.Conflicto(
                    $"No se puede cambiar el tipo a uno que no almacena: hay {equipos} "
                    + "equipos en esta ubicacion. Traspasalos primero.");
            }
        }

        ubicacion.Codigo = codigo;
        ubicacion.Nombre = cambio.Nombre.Trim();
        ubicacion.Tipo = cambio.Tipo;
        ubicacion.Domicilio = Vacio(cambio.Domicilio);
        ubicacion.Telefono = Vacio(cambio.Telefono);
        ubicacion.Latitud = cambio.Latitud;
        ubicacion.Longitud = cambio.Longitud;

        return await GuardarAsync(ubicacion, ct);
    }

    public async Task<Resultado<UbicacionDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var ubicacion = await bd.Ubicaciones.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (ubicacion is null)
        {
            return Resultado<UbicacionDto>.NoEncontrado("La ubicacion no existe.");
        }

        ubicacion.Activo = activo;

        return await GuardarAsync(ubicacion, ct);
    }

    private static bool Almacena(TipoUbicacion tipo)
        => tipo is TipoUbicacion.Bodega or TipoUbicacion.Patio;

    private async Task<Resultado<UbicacionDto>> GuardarAsync(
        Ubicacion ubicacion, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<UbicacionDto>.Conflicto(
                $"Ya existe una ubicacion con el codigo '{ubicacion.Codigo}'.");
        }

        return Resultado<UbicacionDto>.Ok((await ObtenerAsync(ubicacion.Id, ct))!);
    }

    private static string? Validar(AltaUbicacion alta)
        => string.IsNullOrWhiteSpace(alta.Codigo) ? "El codigo es obligatorio."
            : string.IsNullOrWhiteSpace(alta.Nombre) ? "El nombre es obligatorio."
            : !Enum.IsDefined(alta.Tipo) ? "El tipo de ubicacion no es valido."
            // El CHECK de la base exige que las dos coordenadas vengan o falten juntas.
            : (alta.Latitud is null) != (alta.Longitud is null)
                ? "La latitud y la longitud van juntas: las dos o ninguna."
            : alta.Telefono is not null
              && !string.IsNullOrWhiteSpace(alta.Telefono)
              && !Dominio.Comun.FormatoTelefono.EsValido(alta.Telefono.Trim())
                ? Dominio.Comun.FormatoTelefono.Explicacion
            : null;

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
