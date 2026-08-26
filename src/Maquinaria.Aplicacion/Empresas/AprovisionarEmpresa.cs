using Maquinaria.Aplicacion.Correo;
using Maquinaria.Dominio.Comun;
using Maquinaria.Dominio.Plataforma;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Empresas;

/// <summary>
/// Da de alta una empresa: la registra, le crea y migra su base, siembra su
/// administrador y le manda la invitacion.
///
/// ES UNA SECUENCIA, NO UN INSERT, y no puede ser atomica: PostgreSQL no permite
/// CREATE DATABASE dentro de una transaccion. Hay entonces una ventana en la que la
/// fila del tenant existe y su base no, y por eso existe
/// <see cref="EstadoAprovisionamiento"/>: deja el registro REINTENTABLE en lugar de un
/// huerfano que haya que borrar a mano.
///
///     1. INSERT tenant + suscripcion       Pendiente
///     2. CREATE DATABASE                   Creando
///     3. Migraciones (siembran permisos y roles)
///     4. Primer administrador + invitacion
///     5. version_esquema                   Lista
///     6. Correo                            best-effort
///
/// El paso 6 NO puede tumbar los cinco anteriores. Si el correo no sale, la empresa
/// esta aprovisionada y lo que falta es reenviar la invitacion, no repetir el alta.
/// </summary>
public sealed class AprovisionarEmpresa(
    IRegistroTenants registro,
    IAprovisionadorBaseDatos bases,
    ISembradorAdministrador sembrador,
    IEnviadorCorreo correo,
    IPlantillasCorreo plantillas,
    IDirectorioTenants directorio,
    ILogger<AprovisionarEmpresa> log)
{
    public async Task<ResultadoAlta> EjecutarAsync(AltaDeEmpresa alta, CancellationToken ct)
    {
        var slug = FormatoSlug.Normalizar(alta.Slug);

        // ---------- validaciones que no cuestan nada y evitan basura ----------
        // El FORMATO primero: es el fallo mas basico y el unico que, sin esta
        // comprobacion, llegaba hasta el INSERT y salia como un 500 generico en lugar
        // de decirle a quien captura que esta mal.
        if (!FormatoSlug.EsValido(slug))
        {
            return ResultadoAlta.Rechazado(FormatoSlug.Explicacion);
        }

        if (SlugsReservados.EstaReservado(slug))
        {
            return ResultadoAlta.Rechazado(
                $"El identificador '{slug}' esta reservado por la plataforma.");
        }

        // El RFC y el telefono son OPCIONALES: vacio o nulo se guarda como nulo. Lo que
        // no se acepta es basura donde deberia haber un dato. Sus columnas son text sin
        // CHECK, asi que este es el unico sitio donde se puede rechazar; validarlo solo
        // en el frontend es teatro porque cualquiera manda el POST directo.
        //
        // UN RECHAZO POR CAMPO, con la Explicacion de su formato: "los datos son
        // invalidos" obliga a adivinar cual de los tres.
        var rfc = string.IsNullOrWhiteSpace(alta.Rfc) ? null : FormatoRfc.Normalizar(alta.Rfc);

        if (rfc is not null && !FormatoRfc.EsValido(rfc))
        {
            return ResultadoAlta.Rechazado(FormatoRfc.Explicacion);
        }

        // NORMALIZADO, o sea solo los digitos. Se valida y se guarda igual, asi que dos
        // capturas del mismo telefono con distinto formato quedan como el mismo valor.
        var telefono = string.IsNullOrWhiteSpace(alta.Telefono)
            ? null
            : FormatoTelefono.Normalizar(alta.Telefono);

        if (telefono is not null && !FormatoTelefono.EsValido(telefono))
        {
            return ResultadoAlta.Rechazado(FormatoTelefono.Explicacion);
        }

        // El correo del administrador SI es obligatorio: es el unico buzon al que puede
        // llegar la invitacion, y una direccion mal escrita deja una empresa aprovisionada
        // a la que nadie puede entrar.
        //
        // El IsNullOrWhiteSpace va PRIMERO y no es redundante con EsValido: corta antes de
        // que Normalizar toque un nulo, que es lo unico de este bloque que saldria como un
        // 500 en lugar de como un 400.
        if (string.IsNullOrWhiteSpace(alta.CorreoAdministrador)
            || !FormatoCorreo.EsValido(alta.CorreoAdministrador))
        {
            return ResultadoAlta.Rechazado(FormatoCorreo.Explicacion);
        }

        var correoAdministrador = FormatoCorreo.Normalizar(alta.CorreoAdministrador);

        var nombreBd = NombreBdDesdeSlug(slug);

        if (await registro.ExisteSlugAsync(slug, ct))
        {
            return ResultadoAlta.Rechazado($"Ya existe una empresa con el identificador '{slug}'.");
        }

        var plan = await registro.BuscarPlanPorCodigoAsync(alta.CodigoPlan, ct);

        if (plan is null || !plan.Activo)
        {
            return ResultadoAlta.Rechazado($"El plan '{alta.CodigoPlan}' no existe o esta inactivo.");
        }

        // ---------- 1. tenant + suscripcion, juntos ----------
        // Los textos libres se RECORTAN antes de guardar. `IsNullOrWhiteSpace` ya rechaza el
        // campo que es solo espacios, pero no toca `"  Acme  "`: eso entraba a la base con
        // los espacios y de ahi salia al correo de invitacion y a la tabla del panel, donde
        // nadie los ve pero desalinean y hacen que dos razones sociales iguales no lo sean
        // al comparar. Recortar no es reformatear: no cambia lo que alguien escribio.
        var tenant = new Tenant
        {
            Slug = slug,
            NombreBd = nombreBd,
            RazonSocial = alta.RazonSocial.Trim(),
            NombreComercial = alta.NombreComercial?.Trim(),
            Rfc = rfc,
            Telefono = telefono,
            CorreoContacto = alta.CorreoContacto,
            Estado = EstadoTenant.Prueba,
            EstadoAprovisionamiento = EstadoAprovisionamiento.Pendiente,
        };

        // Sin suscripcion no hay modulos contratados, y sin modulos la empresa no puede
        // entrar a nada. Va en la misma transaccion que el tenant.
        var suscripcion = new Suscripcion
        {
            TenantId = tenant.Id,
            PlanId = plan.Id,
            Inicio = DateTime.UtcNow,
            Fin = null,
            Estado = EstadoSuscripcion.Prueba,
        };

        try
        {
            await registro.CrearAsync(tenant, suscripcion, ct);
        }
        catch (Exception e) when (registro.EsColisionDeUnicidad(e))
        {
            // CARRERA entre la comprobacion de ExisteSlugAsync y este INSERT: dos altas
            // simultaneas del mismo slug. El indice unico es lo que de verdad lo impide;
            // aqui solo se traduce a un rechazo en lugar de a un 500.
            //
            // Es el mismo razonamiento que el EXCLUDE de suscripcion: la comprobacion
            // previa no sirve bajo concurrencia, el constraint si.
            log.LogWarning(e, "Colision de unicidad al registrar el tenant {Slug}.", slug);

            return ResultadoAlta.Rechazado(
                $"Ya existe una empresa con el identificador '{slug}'.");
        }

        log.LogInformation(
            "Tenant {Slug} registrado con plan {Plan}. Aprovisionando {NombreBd}.",
            slug, plan.Codigo, nombreBd);

        return await EjecutarSecuenciaAsync(
            tenant.Id, slug, nombreBd, alta.RazonSocial,
            correoAdministrador, alta.NombreAdministrador.Trim(), ct);
    }

    /// <summary>
    /// REINTENTA un alta que quedo en <see cref="EstadoAprovisionamiento.Fallida"/>.
    ///
    /// No repite el paso 1 —el tenant y su suscripcion ya existen y son lo unico atomico
    /// de la secuencia— sino los pasos 2 a 6, que son los idempotentes: ExisteBaseAsync
    /// antes del CREATE, Migrate() que ya lo es de por si, y un sembrador que reusa el
    /// usuario y no deja dos invitaciones vigentes.
    ///
    /// SOLO desde Fallida, y eso no es cortesia: reintentar sobre una empresa que ya esta
    /// Lista reemitiria la invitacion de su administrador, y quien tuviera acceso al panel
    /// podria tomar esa cuenta sin conocer su contrasena. Sobre una en Creando se
    /// solaparia con el intento que todavia corre.
    /// </summary>
    public async Task<ResultadoAlta> ReintentarAsync(
        string slug, ReintentoDeAlta reintento, CancellationToken ct)
    {
        var normalizado = FormatoSlug.Normalizar(slug);

        if (!FormatoSlug.EsValido(normalizado))
        {
            return ResultadoAlta.Rechazado(FormatoSlug.Explicacion);
        }

        if (string.IsNullOrWhiteSpace(reintento.CorreoAdministrador)
            || string.IsNullOrWhiteSpace(reintento.NombreAdministrador))
        {
            return ResultadoAlta.Rechazado(
                "Correo y nombre del administrador son obligatorios.");
        }

        // MISMA validacion de formato que el alta, y por la misma razon: este correo es el
        // que recibe la liga de invitacion. Un reintento con la direccion mal escrita vuelve
        // a dejar la empresa en un estado del que nadie puede entrar.
        var correoReintento = FormatoCorreo.Normalizar(reintento.CorreoAdministrador);

        if (!FormatoCorreo.EsValido(correoReintento))
        {
            return ResultadoAlta.Rechazado(FormatoCorreo.Explicacion);
        }

        var tenant = await registro.BuscarPorSlugAsync(normalizado, ct);

        if (tenant is null)
        {
            return ResultadoAlta.Rechazado($"No existe una empresa con el identificador '{normalizado}'.");
        }

        if (tenant.EliminadoEn is not null)
        {
            return ResultadoAlta.Rechazado(
                $"La empresa '{normalizado}' esta dada de baja. Reactivarla es otra operacion.");
        }

        if (tenant.EstadoAprovisionamiento != EstadoAprovisionamiento.Fallida)
        {
            return ResultadoAlta.Rechazado(
                $"Solo se reintenta un alta en Fallida. '{normalizado}' esta en "
                + $"{tenant.EstadoAprovisionamiento}.");
        }

        // REVALIDACION DEL NOMBRE DE LA BASE antes de que llegue a concatenarse en un
        // CREATE DATABASE. Es la restriccion 2 del aprovisionamiento: los identificadores
        // SQL no se parametrizan, asi que el formato se comprueba en C# y no se confia en
        // el CHECK de la tabla. Que el valor venga de nuestra propia base central no lo
        // exime: el reintento es el unico camino que toma un nombre_bd ya almacenado en
        // lugar de derivarlo del slug recien validado.
        var nombreBd = NombreBdDesdeSlug(normalizado);

        if (tenant.NombreBd != nombreBd)
        {
            log.LogError(
                "El tenant {Slug} tiene nombre_bd inesperado. No se reintenta.", normalizado);

            return ResultadoAlta.Rechazado(
                "El registro de la empresa es inconsistente. Hay que revisarlo a mano.");
        }

        log.LogInformation("Reintentando el aprovisionamiento de {Slug}.", normalizado);

        return await EjecutarSecuenciaAsync(
            tenant.Id, normalizado, nombreBd, tenant.RazonSocial,
            correoReintento, reintento.NombreAdministrador.Trim(), ct);
    }

    /// <summary>
    /// Los pasos 2 a 6, que son los idempotentes y por tanto los reintentables. UNA SOLA
    /// COPIA para el alta y para el reintento: si el reintento tuviera la suya, cualquier
    /// arreglo de la secuencia habria que hacerlo dos veces.
    /// </summary>
    private async Task<ResultadoAlta> EjecutarSecuenciaAsync(
        Guid tenantId, string slug, string nombreBd, string razonSocial,
        string correoAdministrador, string nombreAdministrador, CancellationToken ct)
    {
        try
        {
            // ---------- 2. la base ----------
            await registro.CambiarEstadoAprovisionamientoAsync(
                tenantId, EstadoAprovisionamiento.Creando, ct);

            if (await bases.ExisteBaseAsync(nombreBd, ct))
            {
                // Solo puede pasar reintentando un alta que fallo despues del CREATE.
                log.LogWarning("La base {NombreBd} ya existia. Se reutiliza.", nombreBd);
            }
            else
            {
                await bases.CrearBaseAsync(nombreBd, ct);
            }

            // ---------- 3. migraciones, que ademas siembran permisos y roles ----------
            var version = await bases.MigrarAsync(nombreBd, ct);

            // ---------- 4. el primer administrador ----------
            // Se usa el correo que DEVUELVE el sembrador y no el que se le paso: si la
            // base ya tenia un administrador con acceso total —reintento—, gana ese, y la
            // liga tiene que ir a su buzon y no al que se capturo.
            var admin = await sembrador.CrearAdministradorAsync(
                nombreBd, correoAdministrador, nombreAdministrador, ct);

            // ---------- 5. lista ----------
            await registro.MarcarListaAsync(tenantId, version, ct);

            // La cache pudo haber guardado este tenant como no-operable si algo lo
            // consulto mientras se aprovisionaba.
            directorio.Invalidar(tenantId, slug);

            log.LogInformation(
                "Empresa {Slug} aprovisionada. Esquema {Version}.", slug, version);

            // ---------- 6. correo, best-effort ----------
            var liga = plantillas.LigaDeInvitacion(slug, admin.TokenEnClaro);
            var mensaje = plantillas.Invitacion(admin.Correo, razonSocial, liga);
            var envio = await correo.EnviarAsync(mensaje, ct);

            if (!envio.Enviado)
            {
                log.LogError(
                    "Empresa {Slug} aprovisionada pero la invitacion NO se envio: {Motivo}. "
                    + "Hay que reenviarla.",
                    slug, envio.Detalle);
            }

            return ResultadoAlta.Exito(new EmpresaAprovisionada(
                tenantId, slug, nombreBd, version, envio.Enviado,
                plantillas.DevuelveLigaEnRespuesta ? liga : null));
        }
        catch (Exception e)
        {
            // Fallida, no borrado. El registro queda reintentable en lugar de dejar un
            // huerfano que alguien tenga que limpiar a mano para poder volver a intentar.
            log.LogError(e, "Fallo el aprovisionamiento de {Slug}. Queda reintentable.", slug);

            try
            {
                await registro.CambiarEstadoAprovisionamientoAsync(
                    tenantId, EstadoAprovisionamiento.Fallida, CancellationToken.None);
            }
            catch (Exception eEstado)
            {
                // Si ni esto se puede escribir, el tenant se queda en Creando, que el
                // endpoint de salud reporta como aprovisionamiento colgado.
                log.LogCritical(
                    eEstado, "Tampoco se pudo marcar {Slug} como Fallida.", slug);
            }

            return ResultadoAlta.Fallo(
                "El aprovisionamiento no se completo. El registro quedo reintentable.");
        }
    }

    /// <summary>
    /// Guiones a guiones bajos: un nombre de base con guiones obliga a entrecomillar el
    /// identificador en cada sentencia.
    ///
    /// El prefijo se duplica aqui y en FabricaConexionesEmpresa a proposito: Aplicacion
    /// no depende de Infraestructura. Una prueba fija que coinciden.
    /// </summary>
    public const string PrefijoBaseDatos = "maquinaria_";

    public static string NombreBdDesdeSlug(string slug)
        => PrefijoBaseDatos + slug.Trim().ToLowerInvariant().Replace('-', '_');
}

/// <summary>
/// Distingue los tres desenlaces. Un rechazo por validacion NO es un fallo del sistema
/// y no debe verse igual en el log ni en la respuesta HTTP.
/// </summary>
public readonly record struct ResultadoAlta(
    bool Correcto, bool EsRechazo, string? Motivo, EmpresaAprovisionada? Empresa)
{
    public static ResultadoAlta Exito(EmpresaAprovisionada e) => new(true, false, null, e);

    /// <summary>Datos invalidos. 400, no 500.</summary>
    public static ResultadoAlta Rechazado(string motivo) => new(false, true, motivo, null);

    /// <summary>Algo se rompio a medio camino. Reintentable.</summary>
    public static ResultadoAlta Fallo(string motivo) => new(false, false, motivo, null);
}
