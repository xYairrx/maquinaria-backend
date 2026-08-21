using Maquinaria.Aplicacion.Correo;
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
        var tenant = new Tenant
        {
            Slug = slug,
            NombreBd = nombreBd,
            RazonSocial = alta.RazonSocial,
            NombreComercial = alta.NombreComercial,
            Rfc = alta.Rfc,
            Telefono = alta.Telefono,
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

        try
        {
            // ---------- 2. la base ----------
            await registro.CambiarEstadoAprovisionamientoAsync(
                tenant.Id, EstadoAprovisionamiento.Creando, ct);

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
            var token = await sembrador.CrearAdministradorAsync(
                nombreBd, alta.CorreoAdministrador, alta.NombreAdministrador, ct);

            // ---------- 5. lista ----------
            await registro.MarcarListaAsync(tenant.Id, version, ct);

            // La cache pudo haber guardado este tenant como no-operable si algo lo
            // consulto mientras se aprovisionaba.
            directorio.Invalidar(tenant.Id, slug);

            log.LogInformation(
                "Empresa {Slug} aprovisionada. Esquema {Version}.", slug, version);

            // ---------- 6. correo, best-effort ----------
            var liga = plantillas.LigaDeInvitacion(slug, token);
            var mensaje = plantillas.Invitacion(alta.CorreoAdministrador, alta.RazonSocial, liga);
            var envio = await correo.EnviarAsync(mensaje, ct);

            if (!envio.Enviado)
            {
                log.LogError(
                    "Empresa {Slug} aprovisionada pero la invitacion NO se envio: {Motivo}. "
                    + "Hay que reenviarla.",
                    slug, envio.Detalle);
            }

            return ResultadoAlta.Exito(new EmpresaAprovisionada(
                tenant.Id, slug, nombreBd, version, envio.Enviado,
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
                    tenant.Id, EstadoAprovisionamiento.Fallida, CancellationToken.None);
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
