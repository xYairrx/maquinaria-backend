using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Organizacion;
using Maquinaria.Dominio.Comun;
using Maquinaria.Dominio.Organizacion;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Organizacion;

internal sealed class ServicioTrabajadoresEf(ContextoEmpresa bd) : IServicioTrabajadores
{
    public async Task<Pagina<TrabajadorDto>> ListarAsync(
        FiltroTrabajadores filtro, CancellationToken ct)
    {
        var consulta = bd.Trabajadores.AsNoTracking();

        if (filtro.PuestoId is Guid puesto)
        {
            consulta = consulta.Where(t => t.PuestoId == puesto);
        }

        if (filtro.UbicacionId is Guid ubicacion)
        {
            consulta = consulta.Where(t => t.UbicacionId == ubicacion);
        }

        if (filtro.Estado is EstadoTrabajador estado)
        {
            consulta = consulta.Where(t => t.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            // Nombre, apellidos y numero de empleado: son las tres formas en que alguien
            // busca a una persona en el mostrador.
            consulta = consulta.Where(t =>
                EF.Functions.ILike(t.Nombre, $"%{texto}%")
                || (t.Apellidos != null && EF.Functions.ILike(t.Apellidos, $"%{texto}%"))
                || EF.Functions.ILike(t.NumeroEmpleado, $"%{texto}%"));
        }

        // `Activo` del filtro base se lee como «no dado de baja»: es lo que espera quien
        // marca la casilla, y el trabajador no tiene columna Activo.
        if (filtro.Activo is bool activo)
        {
            consulta = activo
                ? consulta.Where(t => t.Estado != EstadoTrabajador.Baja)
                : consulta.Where(t => t.Estado == EstadoTrabajador.Baja);
        }

        var total = await consulta.LongCountAsync(ct);

        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("numero", false) => consulta.OrderBy(t => t.NumeroEmpleado),
            ("numero", true) => consulta.OrderByDescending(t => t.NumeroEmpleado),
            (_, true) => consulta.OrderByDescending(t => t.Nombre)
                                 .ThenByDescending(t => t.Apellidos),
            _ => consulta.OrderBy(t => t.Nombre).ThenBy(t => t.Apellidos),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(t => Proyectar(t))
            .ToListAsync(ct);

        return new Pagina<TrabajadorDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    private static TrabajadorDto Proyectar(Trabajador t) => new(
        t.Id,
        t.NumeroEmpleado,
        t.Nombre,
        t.Apellidos,
        t.PuestoId,
        t.Puesto!.Nombre,
        t.UbicacionId,
        t.Ubicacion == null ? null : t.Ubicacion.Nombre,
        t.UsuarioId,
        t.Telefono,
        t.Correo,
        t.Estado,
        t.FechaIngreso,
        t.FechaBaja);

    public Task<TrabajadorDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Trabajadores
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => Proyectar(t))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<TrabajadorDto>> CrearAsync(
        AltaTrabajador alta, CancellationToken ct)
    {
        if (await ValidarAsync(alta, null, ct) is string invalido)
        {
            return Resultado<TrabajadorDto>.Invalido(invalido);
        }

        var numero = alta.NumeroEmpleado.Trim().ToUpperInvariant();

        if (await bd.Trabajadores.AnyAsync(t => t.NumeroEmpleado == numero, ct))
        {
            return Resultado<TrabajadorDto>.Conflicto(
                $"Ya existe un trabajador con el numero '{numero}'.");
        }

        var trabajador = new Trabajador
        {
            NumeroEmpleado = numero,
            Nombre = alta.Nombre.Trim(),
            Apellidos = Vacio(alta.Apellidos),
            PuestoId = alta.PuestoId,
            UbicacionId = alta.UbicacionId,
            UsuarioId = alta.UsuarioId,
            Telefono = Normalizar(alta.Telefono, FormatoTelefono.Normalizar),
            Correo = Normalizar(alta.Correo, FormatoCorreo.Normalizar),
            FechaIngreso = alta.FechaIngreso,
        };

        bd.Trabajadores.Add(trabajador);

        return await GuardarAsync(trabajador, ct);
    }

    public async Task<Resultado<TrabajadorDto>> EditarAsync(
        Guid id, AltaTrabajador cambio, CancellationToken ct)
    {
        if (await ValidarAsync(cambio, id, ct) is string invalido)
        {
            return Resultado<TrabajadorDto>.Invalido(invalido);
        }

        var trabajador = await bd.Trabajadores.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (trabajador is null)
        {
            return Resultado<TrabajadorDto>.NoEncontrado("El trabajador no existe.");
        }

        var numero = cambio.NumeroEmpleado.Trim().ToUpperInvariant();

        if (await bd.Trabajadores.AnyAsync(t => t.NumeroEmpleado == numero && t.Id != id, ct))
        {
            return Resultado<TrabajadorDto>.Conflicto(
                $"Ya existe otro trabajador con el numero '{numero}'.");
        }

        trabajador.NumeroEmpleado = numero;
        trabajador.Nombre = cambio.Nombre.Trim();
        trabajador.Apellidos = Vacio(cambio.Apellidos);
        trabajador.PuestoId = cambio.PuestoId;
        trabajador.UbicacionId = cambio.UbicacionId;
        trabajador.UsuarioId = cambio.UsuarioId;
        trabajador.Telefono = Normalizar(cambio.Telefono, FormatoTelefono.Normalizar);
        trabajador.Correo = Normalizar(cambio.Correo, FormatoCorreo.Normalizar);
        trabajador.FechaIngreso = cambio.FechaIngreso;
        trabajador.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(trabajador, ct);
    }

    public async Task<Resultado<TrabajadorDto>> CambiarEstadoAsync(
        Guid id, CambioEstadoTrabajador cambio, CancellationToken ct)
    {
        if (!Enum.IsDefined(cambio.Estado))
        {
            return Resultado<TrabajadorDto>.Invalido("El estado no es valido.");
        }

        var esBaja = cambio.Estado == EstadoTrabajador.Baja;

        // El CHECK trabajador_baja_coherente dice `(estado = 3) = (fecha_baja IS NOT NULL)`.
        // Las dos mitades, con mensaje.
        if (esBaja && cambio.FechaBaja is null)
        {
            return Resultado<TrabajadorDto>.Invalido(
                "Dar de baja exige la fecha de baja.");
        }

        if (!esBaja && cambio.FechaBaja is not null)
        {
            return Resultado<TrabajadorDto>.Invalido(
                "La fecha de baja solo va cuando el estado es Baja.");
        }

        var trabajador = await bd.Trabajadores.FirstOrDefaultAsync(t => t.Id == id, ct);

        if (trabajador is null)
        {
            return Resultado<TrabajadorDto>.NoEncontrado("El trabajador no existe.");
        }

        if (cambio.FechaBaja is DateOnly baja
            && trabajador.FechaIngreso is DateOnly ingreso
            && baja < ingreso)
        {
            return Resultado<TrabajadorDto>.Invalido(
                "La fecha de baja no puede ser anterior a la de ingreso.");
        }

        trabajador.Estado = cambio.Estado;
        trabajador.FechaBaja = cambio.FechaBaja;
        trabajador.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(trabajador, ct);
    }

    private async Task<Resultado<TrabajadorDto>> GuardarAsync(
        Trabajador trabajador, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            // Dos unicos posibles: el numero de empleado y el enlace a la cuenta. El mensaje
            // no adivina cual: dice los dos, que es mas util que uno equivocado.
            return Resultado<TrabajadorDto>.Conflicto(
                "El numero de empleado ya existe, o esa cuenta ya esta ligada a otra persona.");
        }

        return Resultado<TrabajadorDto>.Ok((await ObtenerAsync(trabajador.Id, ct))!);
    }

    private async Task<string?> ValidarAsync(
        AltaTrabajador alta, Guid? id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(alta.NumeroEmpleado))
        {
            return "El numero de empleado es obligatorio.";
        }

        if (string.IsNullOrWhiteSpace(alta.Nombre))
        {
            return "El nombre es obligatorio.";
        }

        // Los dos formatos salen de Dominio/Comun: las columnas son text sin CHECK, asi que
        // esto es la unica defensa. Ver la nota de FormatoTelefono.
        if (!string.IsNullOrWhiteSpace(alta.Telefono)
            && !FormatoTelefono.EsValido(FormatoTelefono.Normalizar(alta.Telefono)))
        {
            return FormatoTelefono.Explicacion;
        }

        if (!string.IsNullOrWhiteSpace(alta.Correo)
            && !FormatoCorreo.EsValido(FormatoCorreo.Normalizar(alta.Correo)))
        {
            return FormatoCorreo.Explicacion;
        }

        if (!await bd.Puestos.AnyAsync(p => p.Id == alta.PuestoId, ct))
        {
            return "El puesto no existe.";
        }

        if (alta.UbicacionId is Guid ubicacion
            && !await bd.Ubicaciones.AnyAsync(u => u.Id == ubicacion, ct))
        {
            return "La ubicacion no existe.";
        }

        if (alta.UsuarioId is Guid usuario)
        {
            if (!await bd.Usuarios.AnyAsync(u => u.Id == usuario, ct))
            {
                return "La cuenta de usuario no existe.";
            }

            // El indice unico parcial lo impide igual, pero como excepcion. Aqui dice quien.
            var ligado = await bd.Trabajadores
                .Where(t => t.UsuarioId == usuario && (id == null || t.Id != id))
                .Select(t => t.NumeroEmpleado)
                .FirstOrDefaultAsync(ct);

            if (ligado is not null)
            {
                return $"Esa cuenta ya esta ligada al trabajador {ligado}.";
            }
        }

        return null;
    }

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string? Normalizar(string? texto, Func<string, string> normalizador)
        => string.IsNullOrWhiteSpace(texto) ? null : normalizador(texto);
}
