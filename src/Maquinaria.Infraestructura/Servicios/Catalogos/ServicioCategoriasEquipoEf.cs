using Maquinaria.Aplicacion.Catalogos;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Dominio.Catalogos;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Catalogos;

/// <summary>
/// El catalogo de categorias contra la base de la empresa.
///
/// ES EL PRIMER SERVICIO DE LA FASE 1 y el patron que copian los demas. Cuatro cosas se
/// repiten en todos y estan comentadas donde ocurren: la proyeccion en el <c>Select</c>, el
/// <c>COUNT</c> antes del <c>Skip/Take</c>, el orden por lista blanca, y la traduccion del
/// rechazo del motor a <see cref="RazonRechazo.Conflicto"/>.
/// </summary>
internal sealed class ServicioCategoriasEquipoEf(ContextoEmpresa bd)
    : IServicioCategoriasEquipo
{
    public async Task<Pagina<CategoriaEquipoDto>> ListarAsync(
        Filtro filtro, CancellationToken ct)
    {
        var consulta = bd.CategoriasEquipo.AsNoTracking();

        // `IncluirEliminados` NO se aplica aqui: categoria_equipo no tiene eliminado_en. El
        // equivalente en este catalogo es `Activo`, y ese si se filtra.
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            // ILIKE y no ToLower().Contains(): ToLower() en el predicado impide usar
            // indice y obliga al motor a bajar cada fila a minusculas.
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.Nombre, $"%{texto}%")
                || EF.Functions.ILike(c.Codigo, $"%{texto}%"));
        }

        if (filtro.Activo is bool activo)
        {
            consulta = consulta.Where(c => c.Activo == activo);
        }

        // EL COUNT VA ANTES DEL Skip/Take y sobre la consulta ya filtrada: es el total de lo
        // que cumple el filtro, no de la pagina. Sin esto la pantalla no puede paginar.
        var total = await consulta.LongCountAsync(ct);

        consulta = Ordenar(consulta, filtro);

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            // La proyeccion va en el Select y la entidad NUNCA se materializa: traerla
            // completa arrastraria la coleccion Tipos entera para contarla en memoria.
            .Select(c => new CategoriaEquipoDto(
                c.Id, c.Codigo, c.Nombre, c.Descripcion, c.Activo, c.Tipos.Count))
            .ToListAsync(ct);

        return new Pagina<CategoriaEquipoDto>(
            filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    /// <summary>
    /// EL ORDEN SALE DE UNA LISTA BLANCA. `Filtro.Orden` es una cadena de la peticion; lo
    /// que no se reconoce cae al orden por defecto en lugar de rechazarse, porque un
    /// parametro de orden mal escrito no debe romper una pantalla.
    ///
    /// Con EF Core no se puede interpolar un nombre de columna en un OrderBy, asi que esa
    /// limitacion del ORM es aqui la defensa contra inyeccion.
    /// </summary>
    private static IQueryable<CategoriaEquipo> Ordenar(
        IQueryable<CategoriaEquipo> consulta, Filtro filtro)
        => (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("codigo", false) => consulta.OrderBy(c => c.Codigo),
            ("codigo", true) => consulta.OrderByDescending(c => c.Codigo),
            ("creado", false) => consulta.OrderBy(c => c.CreadoEn),
            ("creado", true) => consulta.OrderByDescending(c => c.CreadoEn),
            (_, true) => consulta.OrderByDescending(c => c.Nombre),
            _ => consulta.OrderBy(c => c.Nombre),
        };

    public Task<CategoriaEquipoDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.CategoriasEquipo
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoriaEquipoDto(
                c.Id, c.Codigo, c.Nombre, c.Descripcion, c.Activo, c.Tipos.Count))
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<CategoriaEquipoDto>> CrearAsync(
        AltaCategoriaEquipo alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<CategoriaEquipoDto>.Invalido(invalido);
        }

        var codigo = alta.Codigo.Trim().ToUpperInvariant();

        // La comprobacion previa da el mensaje bueno —dice CUAL codigo se repite—. El
        // UNIQUE de la base es lo que de verdad lo garantiza bajo concurrencia, y se
        // traduce abajo.
        if (await bd.CategoriasEquipo.AnyAsync(c => c.Codigo == codigo, ct))
        {
            return Resultado<CategoriaEquipoDto>.Conflicto(
                $"Ya existe una categoria con el codigo '{codigo}'.");
        }

        var categoria = new CategoriaEquipo
        {
            Codigo = codigo,
            Nombre = alta.Nombre.Trim(),
            Descripcion = Vacio(alta.Descripcion),
        };

        bd.CategoriasEquipo.Add(categoria);

        return await GuardarAsync(categoria, ct);
    }

    public async Task<Resultado<CategoriaEquipoDto>> EditarAsync(
        Guid id, AltaCategoriaEquipo cambio, CancellationToken ct)
    {
        if (Validar(cambio) is string invalido)
        {
            return Resultado<CategoriaEquipoDto>.Invalido(invalido);
        }

        // Con seguimiento, no AsNoTracking: esta se va a modificar.
        var categoria = await bd.CategoriasEquipo.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (categoria is null)
        {
            return Resultado<CategoriaEquipoDto>.NoEncontrado("La categoria no existe.");
        }

        var codigo = cambio.Codigo.Trim().ToUpperInvariant();

        // El `c.Id != id` es lo que permite guardar sin cambiar el codigo: sin el, editar
        // solo el nombre chocaria con la propia fila.
        if (await bd.CategoriasEquipo.AnyAsync(c => c.Codigo == codigo && c.Id != id, ct))
        {
            return Resultado<CategoriaEquipoDto>.Conflicto(
                $"Ya existe otra categoria con el codigo '{codigo}'.");
        }

        categoria.Codigo = codigo;
        categoria.Nombre = cambio.Nombre.Trim();
        categoria.Descripcion = Vacio(cambio.Descripcion);

        return await GuardarAsync(categoria, ct);
    }

    public async Task<Resultado<CategoriaEquipoDto>> CambiarActivoAsync(
        Guid id, bool activo, CancellationToken ct)
    {
        var categoria = await bd.CategoriasEquipo.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (categoria is null)
        {
            return Resultado<CategoriaEquipoDto>.NoEncontrado("La categoria no existe.");
        }

        // Desactivar una categoria CON tipos se permite, y es deliberado: no rompe nada
        // —los tipos siguen existiendo y los equipos tambien— y es justo lo que se hace al
        // retirar una linea de negocio. Lo que no se puede es borrarla, y para eso no hay
        // endpoint.
        categoria.Activo = activo;

        return await GuardarAsync(categoria, ct);
    }

    /// <summary>
    /// El guardado y la traduccion del rechazo del motor, en un solo sitio para las tres
    /// escrituras.
    /// </summary>
    private async Task<Resultado<CategoriaEquipoDto>> GuardarAsync(
        CategoriaEquipo categoria, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            // Llega aqui la transaccion que PERDIO la carrera: las dos leyeron «no existe»
            // y las dos insertaron. Sin esto seria un 500.
            return Resultado<CategoriaEquipoDto>.Conflicto(
                $"Ya existe una categoria con el codigo '{categoria.Codigo}'.");
        }

        return Resultado<CategoriaEquipoDto>.Ok(new CategoriaEquipoDto(
            categoria.Id,
            categoria.Codigo,
            categoria.Nombre,
            categoria.Descripcion,
            categoria.Activo,
            categoria.Tipos.Count));
    }

    /// <summary>
    /// Solo forma, y solo lo que la base no puede exigir: <c>codigo</c> y <c>nombre</c> son
    /// <c>NOT NULL</c> pero **no tienen CHECK de no-vacio**, asi que una cadena de espacios
    /// entra sin que el motor diga nada.
    /// </summary>
    private static string? Validar(AltaCategoriaEquipo alta)
        => string.IsNullOrWhiteSpace(alta.Codigo) ? "El codigo es obligatorio."
            : string.IsNullOrWhiteSpace(alta.Nombre) ? "El nombre es obligatorio."
            : alta.Codigo.Trim().Length > 30 ? "El codigo no puede pasar de 30 caracteres."
            : null;

    /// <summary>
    /// Una cadena de espacios se guarda como NULL. Dos filas que dicen «sin descripcion» de
    /// dos formas distintas —cadena vacia y nulo— obligan a comprobar las dos en cada
    /// consulta.
    /// </summary>
    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
