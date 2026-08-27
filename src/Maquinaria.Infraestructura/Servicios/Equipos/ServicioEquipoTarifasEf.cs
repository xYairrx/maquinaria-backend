using System.Linq.Expressions;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Equipos;
using Maquinaria.Dominio.Activos;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Equipos;

internal sealed class ServicioEquipoTarifasEf(ContextoEmpresa bd) : IServicioEquipoTarifas
{
    public async Task<IReadOnlyList<EquipoTarifaDto>> ListarAsync(
        Guid equipoId, bool soloVigentes, CancellationToken ct)
    {
        var consulta = bd.EquipoTarifas
            .AsNoTracking()
            .Where(t => t.EquipoId == equipoId);

        if (soloVigentes)
        {
            consulta = consulta.Where(t =>
                t.VigenciaHasta == null || t.VigenciaHasta > DateTime.UtcNow);
        }

        // SIN PAGINAR, y es una decision: los precios de un equipo son unos pocos por
        // concepto. Paginarlos obligaria a la pantalla del expediente a pedir dos veces para
        // pintar una tabla de seis filas.
        var filas = await consulta
            .OrderBy(t => t.Tarifa!.Nombre)
            .ThenByDescending(t => t.VigenciaDesde)
            .Select(Proyeccion())
            .ToListAsync(ct);

        return filas.Select(f => f.ADto()).ToList();
    }

    /// <summary>
    /// La proyeccion, EN DOS PASOS, y esta es la unica de los servicios que lo necesita.
    ///
    /// El motivo es <c>Unidad.ToString()</c>. El resto de proyecciones se convirtieron en un
    /// arbol de expresion tal cual —EF traduce las navegaciones a JOIN—, pero el nombre de un
    /// enum no existe en la base: la columna guarda el entero, y los rotulos «Hora», «Dia»…
    /// solo viven en el CLR. Meter ese <c>ToString()</c> en el arbol lo rompe con
    /// «could not be translated».
    ///
    /// Asi que la consulta trae la UNIDAD CRUDA —un entero, que si se traduce— y el nombre se
    /// resuelve despues, ya en memoria, con <see cref="Fila.ADto"/>. La diferencia con el
    /// error anterior es donde ocurre cada cosa: antes se materializaban entidades SIN sus
    /// navegaciones y `t.Tarifa!.Nombre` reventaba con NullReferenceException; ahora el JOIN
    /// lo hace la base y lo unico que queda para el cliente es traducir un entero a su
    /// rotulo, que no toca la base.
    /// </summary>
    private static Expression<Func<EquipoTarifa, Fila>> Proyeccion() => t => new Fila(
        t.Id,
        t.EquipoId,
        t.TarifaId,
        t.Tarifa!.Nombre,
        t.Tarifa.Unidad,
        t.ClienteId,
        t.Cliente == null ? null : t.Cliente.RazonSocial,
        t.Precio,
        t.Moneda,
        t.VigenciaDesde,
        t.VigenciaHasta);

    /// <summary>
    /// Lo que devuelve SQL: identico al DTO salvo que <c>Unidad</c> viaja como enum y no como
    /// su nombre. Es interno a este servicio y no sale de aqui.
    /// </summary>
    private sealed record Fila(
        Guid Id,
        Guid EquipoId,
        Guid TarifaId,
        string Tarifa,
        UnidadTarifa Unidad,
        Guid? ClienteId,
        string? Cliente,
        decimal Precio,
        string Moneda,
        DateTime VigenciaDesde,
        DateTime? VigenciaHasta)
    {
        public EquipoTarifaDto ADto() => new(
            Id, EquipoId, TarifaId, Tarifa, Unidad.ToString(), ClienteId, Cliente,
            Precio, Moneda, VigenciaDesde, VigenciaHasta);
    }

    public async Task<Resultado<EquipoTarifaDto>> CrearAsync(
        Guid equipoId, AltaEquipoTarifa alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<EquipoTarifaDto>.Invalido(invalido);
        }

        if (!await bd.Equipos.AnyAsync(e => e.Id == equipoId && e.EliminadoEn == null, ct))
        {
            return Resultado<EquipoTarifaDto>.NoEncontrado("El equipo no existe.");
        }

        var tarifa = await bd.Tarifas
            .Where(t => t.Id == alta.TarifaId)
            .Select(t => new { t.Activo, t.AplicaRenta })
            .FirstOrDefaultAsync(ct);

        if (tarifa is null)
        {
            return Resultado<EquipoTarifaDto>.Invalido("La tarifa no existe.");
        }

        if (!tarifa.Activo)
        {
            // Cargar un precio sobre una tarifa retirada crea un precio que nadie podra
            // usar: la pantalla de captura no ofrece tarifas inactivas.
            return Resultado<EquipoTarifaDto>.Invalido(
                "La tarifa esta retirada del catalogo.");
        }

        if (alta.ClienteId is Guid clienteId
            && !await bd.Clientes.AnyAsync(c => c.Id == clienteId, ct))
        {
            return Resultado<EquipoTarifaDto>.Invalido("El cliente no existe.");
        }

        var precio = new EquipoTarifa
        {
            EquipoId = equipoId,
            TarifaId = alta.TarifaId,
            ClienteId = alta.ClienteId,
            Precio = alta.Precio,
            Moneda = string.IsNullOrWhiteSpace(alta.Moneda)
                ? "MXN"
                : alta.Moneda.Trim().ToUpperInvariant(),
            VigenciaDesde = alta.VigenciaDesde,
            VigenciaHasta = alta.VigenciaHasta,
        };

        bd.EquipoTarifas.Add(precio);

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsTraslape())
        {
            // EL `EXCLUDE` ES LA GARANTIA, y este es el mensaje que la vuelve util. Sin
            // traducirlo, el usuario ve un 500 y el log una traza de Npgsql.
            return Resultado<EquipoTarifaDto>.Conflicto(
                "Ya hay un precio vigente para ese concepto y ese cliente en esas fechas. "
                + "Cierra el anterior antes de cargar el nuevo.");
        }

        return Resultado<EquipoTarifaDto>.Ok(
            (await ListarUnoAsync(precio.Id, ct))!);
    }

    public async Task<Resultado<EquipoTarifaDto>> CerrarAsync(
        Guid equipoId, Guid id, DateTime vigenciaHasta, CancellationToken ct)
    {
        var precio = await bd.EquipoTarifas
            .FirstOrDefaultAsync(t => t.Id == id && t.EquipoId == equipoId, ct);

        if (precio is null)
        {
            return Resultado<EquipoTarifaDto>.NoEncontrado("El precio no existe.");
        }

        // El CHECK equipo_tarifa_vigencia exige fin > inicio. Aqui con mensaje.
        if (vigenciaHasta <= precio.VigenciaDesde)
        {
            return Resultado<EquipoTarifaDto>.Invalido(
                "La fecha de cierre tiene que ser posterior al inicio de la vigencia.");
        }

        if (precio.VigenciaHasta is not null)
        {
            return Resultado<EquipoTarifaDto>.Conflicto(
                "Ese precio ya estaba cerrado.");
        }

        precio.VigenciaHasta = vigenciaHasta;

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsTraslape())
        {
            // Cerrar puede chocar si ya existe otro precio que empieza antes de la fecha de
            // cierre: el rango recortado sigue traslapandose con el.
            return Resultado<EquipoTarifaDto>.Conflicto(
                "Esa fecha de cierre se traslapa con otro precio ya cargado.");
        }

        return Resultado<EquipoTarifaDto>.Ok((await ListarUnoAsync(id, ct))!);
    }

    private async Task<EquipoTarifaDto?> ListarUnoAsync(Guid id, CancellationToken ct)
    {
        var fila = await bd.EquipoTarifas
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(Proyeccion())
            .FirstOrDefaultAsync(ct);

        return fila?.ADto();
    }

    private static string? Validar(AltaEquipoTarifa alta)
        => alta.Precio < 0 ? "El precio no puede ser negativo."
            : alta.Moneda is not null && alta.Moneda.Trim().Length is not (0 or 3)
                ? "La moneda es un codigo ISO 4217 de tres letras, por ejemplo MXN."
            : alta.VigenciaHasta is DateTime hasta && hasta <= alta.VigenciaDesde
                ? "La vigencia final tiene que ser posterior al inicio."
            : null;
}
