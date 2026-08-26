using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Terceros;
using Maquinaria.Dominio.Comun;
using Maquinaria.Dominio.Terceros;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Terceros;

internal sealed class ServicioProveedoresEf(ContextoEmpresa bd) : IServicioProveedores
{
    public async Task<Pagina<ProveedorDto>> ListarAsync(Filtro filtro, CancellationToken ct)
    {
        var consulta = bd.Proveedores.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(p =>
                EF.Functions.ILike(p.RazonSocial, $"%{texto}%")
                || EF.Functions.ILike(p.Codigo, $"%{texto}%")
                || (p.Rfc != null && EF.Functions.ILike(p.Rfc, $"%{texto}%")));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(p => p.Activo == activo);
        }

        var total = await consulta.LongCountAsync(ct);

        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("codigo", false) => consulta.OrderBy(p => p.Codigo),
            ("codigo", true) => consulta.OrderByDescending(p => p.Codigo),
            (_, true) => consulta.OrderByDescending(p => p.RazonSocial),
            _ => consulta.OrderBy(p => p.RazonSocial),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(p => Proyectar(p, bd))
            .ToListAsync(ct);

        return new Pagina<ProveedorDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    private static ProveedorDto Proyectar(Proveedor p, ContextoEmpresa bd) => new(
        p.Id, p.Codigo, p.RazonSocial, p.NombreComercial, p.Rfc, p.Telefono, p.Correo,
        p.Domicilio, p.Contacto, p.Activo,
        bd.OrdenesCompra.Count(o => o.ProveedorId == p.Id));

    public Task<ProveedorDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Proveedores
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => Proyectar(p, bd))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<ProveedorDto>> CrearAsync(
        AltaProveedor alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<ProveedorDto>.Invalido(invalido);
        }

        var codigo = alta.Codigo.Trim().ToUpperInvariant();

        if (await bd.Proveedores.AnyAsync(p => p.Codigo == codigo, ct))
        {
            return Resultado<ProveedorDto>.Conflicto(
                $"Ya existe un proveedor con el codigo '{codigo}'.");
        }

        var proveedor = new Proveedor
        {
            Codigo = codigo,
            RazonSocial = alta.RazonSocial.Trim(),
        };

        Copiar(alta, proveedor);

        bd.Proveedores.Add(proveedor);

        return await GuardarAsync(proveedor, ct);
    }

    public async Task<Resultado<ProveedorDto>> EditarAsync(
        Guid id, AltaProveedor cambio, CancellationToken ct)
    {
        if (Validar(cambio) is string invalido)
        {
            return Resultado<ProveedorDto>.Invalido(invalido);
        }

        var proveedor = await bd.Proveedores.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (proveedor is null)
        {
            return Resultado<ProveedorDto>.NoEncontrado("El proveedor no existe.");
        }

        var codigo = cambio.Codigo.Trim().ToUpperInvariant();

        if (await bd.Proveedores.AnyAsync(p => p.Codigo == codigo && p.Id != id, ct))
        {
            return Resultado<ProveedorDto>.Conflicto(
                $"Ya existe otro proveedor con el codigo '{codigo}'.");
        }

        proveedor.Codigo = codigo;
        proveedor.RazonSocial = cambio.RazonSocial.Trim();

        Copiar(cambio, proveedor);

        proveedor.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(proveedor, ct);
    }

    public async Task<Resultado<ProveedorDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var proveedor = await bd.Proveedores.FirstOrDefaultAsync(p => p.Id == id, ct);

        if (proveedor is null)
        {
            return Resultado<ProveedorDto>.NoEncontrado("El proveedor no existe.");
        }

        proveedor.Activo = activo;
        proveedor.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(proveedor, ct);
    }

    private static void Copiar(AltaProveedor alta, Proveedor proveedor)
    {
        proveedor.NombreComercial = Vacio(alta.NombreComercial);
        proveedor.Rfc = string.IsNullOrWhiteSpace(alta.Rfc) ? null : FormatoRfc.Normalizar(alta.Rfc);
        proveedor.Telefono = string.IsNullOrWhiteSpace(alta.Telefono)
            ? null
            : FormatoTelefono.Normalizar(alta.Telefono);
        proveedor.Correo = string.IsNullOrWhiteSpace(alta.Correo)
            ? null
            : FormatoCorreo.Normalizar(alta.Correo);
        proveedor.Domicilio = Vacio(alta.Domicilio);
        proveedor.Contacto = Vacio(alta.Contacto);
    }

    private async Task<Resultado<ProveedorDto>> GuardarAsync(
        Proveedor proveedor, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<ProveedorDto>.Conflicto(
                $"Ya existe un proveedor con el codigo '{proveedor.Codigo}'.");
        }

        return Resultado<ProveedorDto>.Ok((await ObtenerAsync(proveedor.Id, ct))!);
    }

    private static string? Validar(AltaProveedor alta)
        => string.IsNullOrWhiteSpace(alta.Codigo) ? "El codigo es obligatorio."
            : string.IsNullOrWhiteSpace(alta.RazonSocial) ? "La razon social es obligatoria."
            : !string.IsNullOrWhiteSpace(alta.Rfc)
              && !FormatoRfc.EsValido(FormatoRfc.Normalizar(alta.Rfc)) ? FormatoRfc.Explicacion
            : !string.IsNullOrWhiteSpace(alta.Telefono)
              && !FormatoTelefono.EsValido(FormatoTelefono.Normalizar(alta.Telefono))
                ? FormatoTelefono.Explicacion
            : !string.IsNullOrWhiteSpace(alta.Correo)
              && !FormatoCorreo.EsValido(FormatoCorreo.Normalizar(alta.Correo))
                ? FormatoCorreo.Explicacion
            : null;

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
