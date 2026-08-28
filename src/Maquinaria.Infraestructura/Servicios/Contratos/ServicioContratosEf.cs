using System.Linq.Expressions;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Contratos;
using Maquinaria.Dominio.Comercial;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Contratos;

internal sealed class ServicioContratosEf(ContextoEmpresa bd, IFolios folios)
    : IServicioContratos
{
    /// <summary>
    /// Sin Cancelado: el enum migrado no lo tiene. Ver la nota de IServicioContratos.
    /// </summary>
    private static readonly Dictionary<EstadoContrato, EstadoContrato[]> Transiciones = new()
    {
        [EstadoContrato.Borrador] = [EstadoContrato.Autorizado],
        [EstadoContrato.Autorizado] = [EstadoContrato.Firmado, EstadoContrato.Terminado],
        [EstadoContrato.Firmado] = [EstadoContrato.Terminado],
    };

    public async Task<Pagina<ContratoDto>> ListarAsync(
        FiltroContratos filtro, CancellationToken ct)
    {
        var consulta = bd.Contratos.AsNoTracking();

        if (filtro.ClienteId is Guid cliente)
        {
            consulta = consulta.Where(c => c.ClienteId == cliente);
        }

        if (filtro.Estado is EstadoContrato estado)
        {
            consulta = consulta.Where(c => c.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.Folio, $"%{texto}%")
                || EF.Functions.ILike(c.Cliente!.RazonSocial, $"%{texto}%"));
        }

        var total = await consulta.LongCountAsync(ct);

        var filas = await consulta
            .OrderByDescending(c => c.FechaInicio).ThenByDescending(c => c.Folio)
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(Encabezado())
            .ToListAsync(ct);

        return new Pagina<ContratoDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    /// <summary>
    /// DEVUELVE UN ARBOL DE EXPRESION, NO UN DTO.
    ///
    /// Con la forma anterior —<c>.Select(Encabezado())</c>— EF no sabia traducir la
    /// LLAMADA A METODO y corria la proyeccion EN MEMORIA. Sin <c>Include</c>, las dos
    /// navegaciones que se leen aqui —renta y cliente— llegaban en nulo y reventaban con
    /// <c>NullReferenceException</c> en cuanto hubiera un contrato.
    ///
    /// Como expresion, EF las traduce a dos INNER JOIN en el mismo SELECT: las dos son
    /// obligatorias —un contrato siempre cuelga de una renta y de un cliente—.
    ///
    /// Se busco tambien el otro defecto de esta familia —un <c>ToString()</c> de enum dentro
    /// del <c>Select</c>, que revienta incluso con la tabla vacia— y aqui no hay ninguno.
    /// </summary>
    private static Expression<Func<Contrato, ContratoDto>> Encabezado() => c => new ContratoDto(
        c.Id, c.Folio, c.RentaId, c.Renta!.Folio,
        c.ClienteId, c.Cliente!.RazonSocial,
        c.FechaInicio, c.FechaFin, c.Deposito, c.Estado, c.FirmadoEn, c.Notas,
        // `Array.Empty` y no `[]`: una EXPRESION DE COLECCION no cabe en un arbol de
        // expresion —error CS9175—. Las clausulas van en una segunda consulta.
        Array.Empty<ContratoClausulaDto>());

    public Task<ContratoDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => ConClausulasAsync(c => c.Id == id, ct);

    public Task<ContratoDto?> PorRentaAsync(Guid rentaId, CancellationToken ct)
        => ConClausulasAsync(c => c.RentaId == rentaId, ct);

    private async Task<ContratoDto?> ConClausulasAsync(
        Expression<Func<Contrato, bool>> filtro, CancellationToken ct)
    {
        var contrato = await bd.Contratos
            .AsNoTracking()
            .Where(filtro)
            .Select(Encabezado())
            .FirstOrDefaultAsync(ct);

        if (contrato is null)
        {
            return null;
        }

        var clausulas = await bd.ContratoClausulas
            .AsNoTracking()
            .Where(c => c.ContratoId == contrato.Id)
            .OrderBy(c => c.Orden)
            .Select(c => new ContratoClausulaDto(c.Id, c.ClausulaId, c.Orden, c.Titulo, c.Texto))
            .ToListAsync(ct);

        return contrato with { Clausulas = clausulas };
    }

    public async Task<Resultado<ContratoDto>> CrearAsync(
        AltaContrato alta, CancellationToken ct)
    {
        if (alta.Deposito < 0)
        {
            return Resultado<ContratoDto>.Invalido("El deposito no puede ser negativo.");
        }

        var renta = await bd.Rentas
            .AsNoTracking()
            .Where(r => r.Id == alta.RentaId)
            .Select(r => new { r.Folio, r.ClienteId, r.Inicio, r.Fin, r.Estado, r.Deposito })
            .FirstOrDefaultAsync(ct);

        if (renta is null)
        {
            return Resultado<ContratoDto>.NoEncontrado("La renta no existe.");
        }

        // UN CONTRATO POR RENTA: el UNIQUE contrato_renta_unica lo garantiza; aqui el mensaje.
        if (await bd.Contratos.AnyAsync(c => c.RentaId == alta.RentaId, ct))
        {
            return Resultado<ContratoDto>.Conflicto(
                $"La renta {renta.Folio} ya tiene contrato.");
        }

        var fechaInicio = alta.FechaInicio ?? DateOnly.FromDateTime(renta.Inicio);
        var fechaFin = alta.FechaFin ?? DateOnly.FromDateTime(renta.Fin);

        if (fechaFin < fechaInicio)
        {
            return Resultado<ContratoDto>.Invalido(
                "La fecha final no puede ser anterior a la inicial.");
        }

        var contrato = new Contrato
        {
            Folio = await folios.SiguienteAsync(TipoDocumento.Contrato, ct),
            RentaId = alta.RentaId,
            ClienteId = renta.ClienteId,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            // El deposito por defecto es el de la renta: es el mismo dinero, y capturarlo dos
            // veces es la forma de que los dos documentos digan cifras distintas.
            Deposito = alta.Deposito == 0 ? renta.Deposito : alta.Deposito,
            Estado = EstadoContrato.Borrador,
            Notas = string.IsNullOrWhiteSpace(alta.Notas) ? null : alta.Notas.Trim(),
        };

        bd.Contratos.Add(contrato);

        // LAS CLAUSULAS SE CONGELAN AQUI: se copian titulo y texto. Si manana se corrige la
        // plantilla, este contrato no cambia.
        var delCatalogo = await bd.Clausulas
            .AsNoTracking()
            .Where(c => c.Activo
                     && (alta.ClausulasDelCatalogo == null
                         || alta.ClausulasDelCatalogo.Count == 0
                             ? c.Obligatoria
                             : alta.ClausulasDelCatalogo.Contains(c.Id)))
            .OrderBy(c => c.Orden)
            .Select(c => new { c.Id, c.Orden, c.Titulo, c.Texto })
            .ToListAsync(ct);

        var orden = 0;

        foreach (var clausula in delCatalogo)
        {
            bd.ContratoClausulas.Add(new ContratoClausula
            {
                ContratoId = contrato.Id,
                // La referencia de donde salio. El texto ya es una copia.
                ClausulaId = clausula.Id,
                // El orden se renumera: el del catalogo puede tener huecos, y el UNIQUE
                // (contrato, orden) no los tolera repetidos.
                Orden = ++orden,
                Titulo = clausula.Titulo,
                Texto = clausula.Texto,
            });
        }

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<ContratoDto>.Conflicto(
                "El folio o la renta ya tienen contrato. Vuelve a intentarlo.");
        }

        return Resultado<ContratoDto>.Ok((await ObtenerAsync(contrato.Id, ct))!);
    }

    public async Task<Resultado<ContratoClausulaDto>> AgregarClausulaAsync(
        Guid contratoId, AltaContratoClausula clausula, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clausula.Titulo))
        {
            return Resultado<ContratoClausulaDto>.Invalido("El titulo es obligatorio.");
        }

        // El CHECK contrato_clausula_texto lo exige en la base.
        if (string.IsNullOrWhiteSpace(clausula.Texto))
        {
            return Resultado<ContratoClausulaDto>.Invalido("El texto es obligatorio.");
        }

        var contrato = await bd.Contratos
            .AsNoTracking()
            .Where(c => c.Id == contratoId)
            .Select(c => new { c.Estado })
            .FirstOrDefaultAsync(ct);

        if (contrato is null)
        {
            return Resultado<ContratoClausulaDto>.NoEncontrado("El contrato no existe.");
        }

        if (contrato.Estado != EstadoContrato.Borrador)
        {
            // El trigger lo rechaza igual. Este mensaje dice por que, y evita el 500.
            return Resultado<ContratoClausulaDto>.Conflicto(
                $"El contrato esta {contrato.Estado}: es un documento con firmas y ya no se "
                + "edita.");
        }

        // CLAUSULA PROPIA: clausula_id va NULO. No existe en el catalogo y el texto es su unico
        // origen — es el caso de lo negociado con ese cliente.
        var nueva = new ContratoClausula
        {
            ContratoId = contratoId,
            ClausulaId = null,
            Orden = clausula.Orden > 0
                ? clausula.Orden
                : await bd.ContratoClausulas
                    .Where(c => c.ContratoId == contratoId)
                    .Select(c => (int?)c.Orden)
                    .MaxAsync(ct) + 1 ?? 1,
            Titulo = clausula.Titulo.Trim(),
            Texto = clausula.Texto.Trim(),
        };

        bd.ContratoClausulas.Add(nueva);

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<ContratoClausulaDto>.Conflicto(
                $"Ya hay una clausula con el orden {nueva.Orden} en este contrato.");
        }

        return Resultado<ContratoClausulaDto>.Ok(new ContratoClausulaDto(
            nueva.Id, null, nueva.Orden, nueva.Titulo, nueva.Texto));
    }

    public async Task<Resultado> QuitarClausulaAsync(
        Guid contratoId, Guid clausulaId, CancellationToken ct)
    {
        var contrato = await bd.Contratos
            .AsNoTracking()
            .Where(c => c.Id == contratoId)
            .Select(c => new { c.Estado })
            .FirstOrDefaultAsync(ct);

        if (contrato is null)
        {
            return Resultado.NoEncontrado("El contrato no existe.");
        }

        if (contrato.Estado != EstadoContrato.Borrador)
        {
            return Resultado.Conflicto(
                $"El contrato esta {contrato.Estado} y ya no se edita.");
        }

        var clausula = await bd.ContratoClausulas
            .FirstOrDefaultAsync(c => c.Id == clausulaId && c.ContratoId == contratoId, ct);

        if (clausula is null)
        {
            return Resultado.NoEncontrado("La clausula no existe en este contrato.");
        }

        bd.ContratoClausulas.Remove(clausula);

        await bd.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado<ContratoDto>> CambiarEstadoAsync(
        Guid id, EstadoContrato estado, CancellationToken ct)
    {
        if (!Enum.IsDefined(estado))
        {
            return Resultado<ContratoDto>.Invalido("El estado no es valido.");
        }

        var contrato = await bd.Contratos.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (contrato is null)
        {
            return Resultado<ContratoDto>.NoEncontrado("El contrato no existe.");
        }

        if (contrato.Estado == estado)
        {
            return Resultado<ContratoDto>.Ok((await ObtenerAsync(id, ct))!);
        }

        if (!Transiciones.TryGetValue(contrato.Estado, out var permitidos)
            || !permitidos.Contains(estado))
        {
            return Resultado<ContratoDto>.Conflicto(
                $"No se puede pasar de {contrato.Estado} a {estado}.");
        }

        // AUTORIZAR EXIGE CLAUSULAS: un contrato sin terminos es un papel en blanco, y una vez
        // autorizado ya no se le pueden agregar.
        if (estado == EstadoContrato.Autorizado
            && !await bd.ContratoClausulas.AnyAsync(c => c.ContratoId == id, ct))
        {
            return Resultado<ContratoDto>.Conflicto(
                "No se puede autorizar un contrato sin clausulas.");
        }

        contrato.Estado = estado;
        contrato.ActualizadoEn = DateTime.UtcNow;

        if (estado == EstadoContrato.Firmado)
        {
            contrato.FirmadoEn = DateTime.UtcNow;
        }

        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion)
            when (excepcion.Estado() == ErroresPostgres.Excepcion)
        {
            // EL TRIGGER contrato_inmutable. Corre en todo UPDATE cuyo estado anterior no sea
            // Borrador, asi que Autorizado → Firmado tambien pasa por el: si el trigger de la
            // base es mas estricto que estas transiciones, gana el.
            return Resultado<ContratoDto>.Conflicto(
                "El contrato ya no se puede modificar: esta fuera de Borrador y el motor lo "
                + "protege.");
        }

        return Resultado<ContratoDto>.Ok((await ObtenerAsync(id, ct))!);
    }
}
