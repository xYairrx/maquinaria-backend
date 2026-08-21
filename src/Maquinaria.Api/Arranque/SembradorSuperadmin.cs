using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Dominio.Plataforma;

namespace Maquinaria.Api.Arranque;

/// <summary>
/// Crea el PRIMER superadministrador, y solo si no hay ninguno.
///
/// Hace falta porque el sistema no tiene registro publico por ningun lado: los
/// tenants los da de alta un superadministrador, y los usuarios de empresa se crean
/// por invitacion. Sin este arranque no habria con que iniciar sesion nunca.
///
/// POR QUE NO UNA SEMILLA EN MIGRACION: una migracion lleva su contenido en el
/// historial para siempre, asi que la contrasena quedaria en el repositorio. Aqui la
/// contrasena viene de SECRETOS —user-secrets en desarrollo, variables de entorno en
/// Railway— y nunca se escribe en disco del repo.
///
/// POR QUE ES SEGURO DEJARLO EN PRODUCCION: solo actua si la tabla esta VACIA. En
/// cuanto existe un superadministrador, este codigo no puede crear otro ni pisar el
/// que hay, asi que no es una puerta trasera.
/// </summary>
internal static class SembradorSuperadmin
{
    public const string Seccion = "Arranque:Superadmin";

    public static async Task SembrarSuperadminAsync(this WebApplication app)
    {
        var log = app.Logger;
        var seccion = app.Configuration.GetSection(Seccion);

        var correo = seccion["Correo"];
        var contrasena = seccion["Contrasena"];
        var nombre = seccion["Nombre"] ?? "Superadministrador";

        if (string.IsNullOrWhiteSpace(correo) || string.IsNullOrWhiteSpace(contrasena))
        {
            log.LogInformation(
                "Sin {Seccion} configurado: no se siembra superadministrador.", Seccion);
            return;
        }

        using var ambito = app.Services.CreateScope();
        var usuarios = ambito.ServiceProvider.GetRequiredService<IUsuariosPlataforma>();
        var hash = ambito.ServiceProvider.GetRequiredService<IHashContrasenas>();

        try
        {
            if (await usuarios.ExisteAlgunoAsync(CancellationToken.None))
            {
                log.LogInformation("Ya existe al menos un superadministrador: no se siembra.");
                return;
            }

            await usuarios.CrearAsync(
                new Usuario
                {
                    Correo = correo.Trim().ToLowerInvariant(),
                    HashContrasena = hash.Hash(contrasena),
                    Nombre = nombre,
                },
                CancellationToken.None);

            log.LogWarning(
                "Superadministrador inicial creado para {Correo}. Cambia su contrasena y "
                + "retira {Seccion} de la configuracion.",
                correo,
                Seccion);
        }
        catch (Exception e)
        {
            // NO se propaga: un problema de red con Neon no debe impedir que la
            // aplicacion arranque. El health check reportara el estado de la base.
            log.LogCritical(e, "Fallo la siembra del superadministrador inicial.");
        }
    }
}
