using System.Linq.Expressions;
using Maquinaria.Aplicacion.Comun;
using Maquinaria.Aplicacion.Terceros;
using Maquinaria.Dominio.Comun;
using Maquinaria.Dominio.Terceros;
using Maquinaria.Infraestructura.Persistencia;
using Maquinaria.Infraestructura.Servicios.Comun;
using Microsoft.EntityFrameworkCore;

namespace Maquinaria.Infraestructura.Servicios.Terceros;

internal sealed class ServicioClientesEf(ContextoEmpresa bd) : IServicioClientes
{
    public async Task<Pagina<ClienteDto>> ListarAsync(
        FiltroClientes filtro, CancellationToken ct)
    {
        var consulta = bd.Clientes.AsNoTracking();

        if (filtro.Estado is EstadoCliente estado)
        {
            consulta = consulta.Where(c => c.Estado == estado);
        }

        if (filtro.Activo is bool activo)
        {
            consulta = activo
                ? consulta.Where(c => c.Estado == EstadoCliente.Activo)
                : consulta.Where(c => c.Estado != EstadoCliente.Activo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var texto = filtro.Texto.Trim();

            // razon_social tiene indice GIN de trigramas, asi que el ILIKE con comodin a los
            // dos lados si lo usa. El codigo y el RFC son las otras dos formas de buscar un
            // cliente en el mostrador.
            consulta = consulta.Where(c =>
                EF.Functions.ILike(c.RazonSocial, $"%{texto}%")
                || EF.Functions.ILike(c.Codigo, $"%{texto}%")
                || (c.Rfc != null && EF.Functions.ILike(c.Rfc, $"%{texto}%")));
        }

        var total = await consulta.LongCountAsync(ct);

        consulta = (filtro.Orden?.Trim().ToLowerInvariant(), filtro.Descendente) switch
        {
            ("codigo", false) => consulta.OrderBy(c => c.Codigo),
            ("codigo", true) => consulta.OrderByDescending(c => c.Codigo),
            (_, true) => consulta.OrderByDescending(c => c.RazonSocial),
            _ => consulta.OrderBy(c => c.RazonSocial),
        };

        var filas = await consulta
            .Skip(filtro.Saltar)
            .Take(filtro.TamanoEfectivo)
            .Select(Proyeccion())
            .ToListAsync(ct);

        return new Pagina<ClienteDto>(filas, filtro.Numero, filtro.TamanoEfectivo, total);
    }

    /// <summary>
    /// DEVUELVE UN ARBOL DE EXPRESION, NO UN DTO.
    ///
    /// Con la forma anterior —<c>.Select(c => Proyectar(c, bd))</c>— EF no sabia traducir la
    /// LLAMADA A METODO, asi que materializaba las entidades y corria la proyeccion EN
    /// MEMORIA. Eso tenia dos costos, y el segundo no estaba anotado en ningun sitio:
    ///
    /// 1. El conteo de rentas se volvia una consulta POR FILA.
    /// 2. **Y TRONABA.** Esa consulta sale sobre la MISMA conexion mientras el lector del
    ///    listado sigue abierto, asi que en cuanto la tabla tiene una fila el endpoint
    ///    responde 500. Con la tabla vacia no se nota: el Select no corre sobre nada.
    ///
    /// El plan de la Fase 1 clasificaba a Clientes como «N+1, no truena». Era falso, y se
    /// comprobo dando de alta la primera fila desde la pantalla.
    ///
    /// NO es <c>static</c> a proposito: captura <c>bd</c> para el conteo.
    /// </summary>
    private Expression<Func<Cliente, ClienteDto>> Proyeccion() => c => new ClienteDto(
        c.Id,
        c.Codigo,
        c.RazonSocial,
        c.NombreComercial,
        c.Rfc,
        c.Telefono,
        c.Correo,
        new ContactoCliente(c.ContactoNombre, c.ContactoPuesto, c.ContactoTelefono, c.ContactoCorreo),
        new DomicilioCliente(
            c.Calle, c.Colonia, c.Municipio, c.EstadoProv, c.CodigoPostal,
            c.Pais, c.Latitud, c.Longitud),
        c.LimiteCredito,
        c.DiasCredito,
        c.DepositoRequerido,
        c.Condiciones,
        c.Estado,
        bd.Rentas.Count(r => r.ClienteId == c.Id));

    public Task<ClienteDto?> ObtenerAsync(Guid id, CancellationToken ct)
        => bd.Clientes
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(Proyeccion())
            .FirstOrDefaultAsync(ct);

    public async Task<Resultado<ClienteDto>> CrearAsync(AltaCliente alta, CancellationToken ct)
    {
        if (Validar(alta) is string invalido)
        {
            return Resultado<ClienteDto>.Invalido(invalido);
        }

        var codigo = alta.Codigo.Trim().ToUpperInvariant();

        if (await bd.Clientes.AnyAsync(c => c.Codigo == codigo, ct))
        {
            return Resultado<ClienteDto>.Conflicto(
                $"Ya existe un cliente con el codigo '{codigo}'.");
        }

        var cliente = new Cliente
        {
            Codigo = codigo,
            RazonSocial = alta.RazonSocial.Trim(),
        };

        Copiar(alta, cliente);

        bd.Clientes.Add(cliente);

        return await GuardarAsync(cliente, ct);
    }

    public async Task<Resultado<ClienteDto>> EditarAsync(
        Guid id, AltaCliente cambio, CancellationToken ct)
    {
        if (Validar(cambio) is string invalido)
        {
            return Resultado<ClienteDto>.Invalido(invalido);
        }

        var cliente = await bd.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cliente is null)
        {
            return Resultado<ClienteDto>.NoEncontrado("El cliente no existe.");
        }

        var codigo = cambio.Codigo.Trim().ToUpperInvariant();

        if (await bd.Clientes.AnyAsync(c => c.Codigo == codigo && c.Id != id, ct))
        {
            return Resultado<ClienteDto>.Conflicto(
                $"Ya existe otro cliente con el codigo '{codigo}'.");
        }

        cliente.Codigo = codigo;
        cliente.RazonSocial = cambio.RazonSocial.Trim();

        Copiar(cambio, cliente);

        cliente.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(cliente, ct);
    }

    public async Task<Resultado<ClienteDto>> CambiarEstadoAsync(
        Guid id, EstadoCliente estado, CancellationToken ct)
    {
        if (!Enum.IsDefined(estado))
        {
            return Resultado<ClienteDto>.Invalido("El estado no es valido.");
        }

        var cliente = await bd.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cliente is null)
        {
            return Resultado<ClienteDto>.NoEncontrado("El cliente no existe.");
        }

        // SUSPENDER O DAR DE BAJA NO TOCA LAS RENTAS ABIERTAS, a proposito: la maquina sigue
        // en la obra y el calendario sigue ocupado. Lo que el estado controla es si se le
        // puede cotizar y rentar de nuevo, y eso lo comprueba quien crea el documento.
        cliente.Estado = estado;
        cliente.ActualizadoEn = DateTime.UtcNow;

        return await GuardarAsync(cliente, ct);
    }

    /// <summary>
    /// Copia los campos capturables. El codigo y la razon social se asignan aparte porque son
    /// los dos obligatorios y ya vienen normalizados.
    /// </summary>
    private static void Copiar(AltaCliente alta, Cliente cliente)
    {
        cliente.NombreComercial = Vacio(alta.NombreComercial);
        cliente.Rfc = string.IsNullOrWhiteSpace(alta.Rfc) ? null : FormatoRfc.Normalizar(alta.Rfc);
        cliente.Telefono = Normalizar(alta.Telefono, FormatoTelefono.Normalizar);
        cliente.Correo = Normalizar(alta.Correo, FormatoCorreo.Normalizar);

        cliente.ContactoNombre = Vacio(alta.Contacto.Nombre);
        cliente.ContactoPuesto = Vacio(alta.Contacto.Puesto);
        cliente.ContactoTelefono = Normalizar(alta.Contacto.Telefono, FormatoTelefono.Normalizar);
        cliente.ContactoCorreo = Normalizar(alta.Contacto.Correo, FormatoCorreo.Normalizar);

        cliente.Calle = Vacio(alta.Domicilio.Calle);
        cliente.Colonia = Vacio(alta.Domicilio.Colonia);
        cliente.Municipio = Vacio(alta.Domicilio.Municipio);
        cliente.EstadoProv = Vacio(alta.Domicilio.EstadoProv);
        cliente.CodigoPostal = Vacio(alta.Domicilio.CodigoPostal);

        // El pais tiene DEFAULT 'MX' en la base y NOT NULL: un cuerpo que no lo manda no debe
        // acabar guardando cadena vacia.
        cliente.Pais = string.IsNullOrWhiteSpace(alta.Domicilio.Pais)
            ? "MX"
            : alta.Domicilio.Pais.Trim().ToUpperInvariant();

        cliente.Latitud = alta.Domicilio.Latitud;
        cliente.Longitud = alta.Domicilio.Longitud;

        cliente.LimiteCredito = alta.LimiteCredito;
        cliente.DiasCredito = alta.DiasCredito;
        cliente.DepositoRequerido = alta.DepositoRequerido;
        cliente.Condiciones = Vacio(alta.Condiciones);
    }

    private async Task<Resultado<ClienteDto>> GuardarAsync(Cliente cliente, CancellationToken ct)
    {
        try
        {
            await bd.SaveChangesAsync(ct);
        }
        catch (DbUpdateException excepcion) when (excepcion.EsViolacionDeUnico())
        {
            return Resultado<ClienteDto>.Conflicto(
                $"Ya existe un cliente con el codigo '{cliente.Codigo}'.");
        }

        return Resultado<ClienteDto>.Ok((await ObtenerAsync(cliente.Id, ct))!);
    }

    /// <summary>
    /// Los tres formatos salen de Dominio/Comun y son la unica defensa: las columnas son
    /// <c>text</c> sin CHECK. Los montos si los cubre el motor, y aqui se rechazan antes para
    /// dar mensaje.
    /// </summary>
    private static string? Validar(AltaCliente alta)
        => string.IsNullOrWhiteSpace(alta.Codigo) ? "El codigo es obligatorio."
            : string.IsNullOrWhiteSpace(alta.RazonSocial) ? "La razon social es obligatoria."
            : !string.IsNullOrWhiteSpace(alta.Rfc)
              && !FormatoRfc.EsValido(FormatoRfc.Normalizar(alta.Rfc)) ? FormatoRfc.Explicacion
            : Telefono(alta.Telefono) ?? Telefono(alta.Contacto.Telefono)
              ?? Correo(alta.Correo) ?? Correo(alta.Contacto.Correo)
              ?? (alta.LimiteCredito < 0 ? "El limite de credito no puede ser negativo."
                  : alta.DiasCredito < 0 ? "Los dias de credito no pueden ser negativos."
                  : alta.DepositoRequerido < 0 ? "El deposito no puede ser negativo."
                  : (alta.Domicilio.Latitud is null) != (alta.Domicilio.Longitud is null)
                      ? "La latitud y la longitud van juntas: las dos o ninguna."
                  : null);

    private static string? Telefono(string? valor)
        => !string.IsNullOrWhiteSpace(valor)
           && !FormatoTelefono.EsValido(FormatoTelefono.Normalizar(valor))
            ? FormatoTelefono.Explicacion
            : null;

    private static string? Correo(string? valor)
        => !string.IsNullOrWhiteSpace(valor)
           && !FormatoCorreo.EsValido(FormatoCorreo.Normalizar(valor))
            ? FormatoCorreo.Explicacion
            : null;

    private static string? Vacio(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string? Normalizar(string? texto, Func<string, string> normalizador)
        => string.IsNullOrWhiteSpace(texto) ? null : normalizador(texto);
}
