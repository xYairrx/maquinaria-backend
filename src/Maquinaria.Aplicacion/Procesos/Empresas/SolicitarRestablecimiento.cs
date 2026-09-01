using System.Diagnostics;
using Maquinaria.Aplicacion.Correo;
using Maquinaria.Aplicacion.Plataforma;
using Maquinaria.Aplicacion.Seguridad;
using Maquinaria.Dominio.Seguridad;
using Microsoft.Extensions.Logging;

namespace Maquinaria.Aplicacion.Empresas;

public readonly record struct PeticionRestablecimiento(string Correo);

/// <summary>
/// "Olvide mi contrasena": recibe empresa y correo, y si hay una cuenta activa detras
/// le manda una liga de un solo uso con una hora de vigencia.
///
/// ESTE CASO DE USO NO DEVUELVE NADA, y esa es la pieza central de su diseno. Un
/// formulario de recuperacion se rellena SIN SESION y admite cualquier direccion, asi
/// que si la respuesta cambiara segun lo encontrado se convertiria en un enumerador:
/// con un diccionario de correos y unas horas, cualquiera sacaria la lista de empleados
/// de un cliente, y probando slugs, la lista de clientes. Devolver void hace imposible
/// que el endpoint ramifique aunque alguien lo intente mas adelante.
///
/// Uniformar el CUERPO no basta: hay que uniformar tambien el TIEMPO y los FALLOS.
///
/// 1. TIEMPO. Las dos ramas no se parecen en nada —una no toca ninguna base y la otra
///    lee, escribe dos veces y llama al proveedor de correo—, asi que se responde
///    siempre al cumplirse un PISO fijo, rellenando con espera lo que haya sobrado.
///    El hash senuelo de IniciarSesionEmpresa se conserva porque es la defensa que no
///    depende de que el piso este bien dimensionado, pero aqui por si solo NO alcanza:
///    imita el costo de un PBKDF2, no el de un POST a Resend.
/// 2. FALLOS. Todo se atrapa y se registra. Una excepcion que suba se volveria un 500,
///    y un 500 que solo aparece cuando la cuenta existe delata igual que un 404.
/// </summary>
public sealed class SolicitarRestablecimiento(
    IContextoTenant contextoTenant,
    Func<IUsuariosEmpresa> usuariosDe,
    IGeneradorTokens tokens,
    IHashContrasenas hash,
    IPlantillasCorreo plantillas,
    IEnviadorCorreo correo,
    ILogger<SolicitarRestablecimiento> log)
{
    /// <summary>
    /// Lo que tarda la respuesta, exista o no la cuenta. Publico porque hay una prueba
    /// que comprueba que las dos ramas lo respetan.
    ///
    /// Dimensionado sobre el camino LARGO —lectura y dos escrituras contra la base de la
    /// empresa, mas el envio, que va acotado por <see cref="TopeEnvio"/>— para que el
    /// relleno nunca sea cero y el piso sea de verdad un piso. Si la base se degrada por
    /// encima de este numero, el relleno se agota y la diferencia vuelve a ser medible:
    /// ese es el limite conocido de esta defensa, y por eso el senuelo sigue ahi.
    /// </summary>
    public static readonly TimeSpan PisoDeRespuesta = TimeSpan.FromMilliseconds(1200);

    /// <summary>
    /// Tope del envio de correo, deliberadamente por debajo del piso.
    ///
    /// Sin el, un proveedor lento estira el camino largo mas alla del piso y el tiempo
    /// vuelve a delatar. Cortarlo es aceptable porque el envio ya es best-effort por
    /// contrato de IEnviadorCorreo: el token quedo emitido y lo que falta es reintentar
    /// el correo, no el restablecimiento.
    /// </summary>
    private static readonly TimeSpan TopeEnvio = TimeSpan.FromMilliseconds(800);

    /// <summary>Perezoso: la base de la empresa no se toca hasta saber que existe.</summary>
    private IUsuariosEmpresa Usuarios => usuariosDe();

    public async Task EjecutarAsync(
        string slug, PeticionRestablecimiento peticion, CancellationToken ct)
    {
        var inicio = Stopwatch.GetTimestamp();

        try
        {
            await TrabajarAsync(slug, peticion, ct);
        }
        catch (Exception e)
        {
            // Se traga TODO, incluida la cancelacion. Dejar subir la excepcion daria un
            // 500 en la rama que hace trabajo y un 202 en la que no: la misma fuga que
            // todo lo demas de esta clase evita, por la puerta de atras.
            log.LogError(e, "Fallo la solicitud de restablecimiento en {Slug}.", slug);
        }
        finally
        {
            await EsperarAlPisoAsync(inicio);
        }
    }

    private async Task TrabajarAsync(
        string slug, PeticionRestablecimiento peticion, CancellationToken ct)
    {
        // IsNullOrWhiteSpace y no Trim() directo: el cuerpo lo deserializa el framework y
        // un JSON sin la propiedad deja la cadena en null pese al tipo no anulable.
        var correoNormalizado = string.IsNullOrWhiteSpace(peticion.Correo)
            ? string.Empty
            : peticion.Correo.Trim().ToLowerInvariant();

        // El middleware ya intento resolver el tenant por el slug de la ruta. Que no lo
        // haya logrado significa que la empresa no existe o no puede operar, y las dos
        // salen por aqui sin distinguirse.
        if (!contextoTenant.EstaResuelto || !contextoTenant.Actual.PuedeOperar)
        {
            hash.VerificarSenuelo(correoNormalizado);
            log.LogInformation("Solicitud de restablecimiento sin destinatario en {Slug}.", slug);

            return;
        }

        var usuario = correoNormalizado.Length == 0
            ? null
            : await Usuarios.BuscarPorCorreoAsync(correoNormalizado, ct);

        // Solo Activo. Un Invitado no tiene nada que restablecer —su liga de invitacion
        // es la que sirve, y mandarle esta otra le daria dos caminos para lo mismo— y
        // Suspendido y Baja no deben poder volver por esta puerta.
        if (usuario is null || usuario.Estado != EstadoUsuario.Activo)
        {
            hash.VerificarSenuelo(correoNormalizado);

            // Se registra que NO hubo destinatario, pero con el mismo mensaje que el
            // caso contrario a nivel de respuesta: el log es interno y aqui si hace
            // falta distinguir para investigar abuso.
            log.LogInformation("Solicitud de restablecimiento sin destinatario en {Slug}.", slug);

            return;
        }

        var token = tokens.Generar();

        await Usuarios.EmitirTokenAsync(
            usuario.Id,
            PropositoToken.RestablecerContrasena,
            token.Hash,
            DateTime.UtcNow.Add(PoliticaRestablecimiento.Vigencia),
            ct);

        var liga = plantillas.LigaDeRestablecimiento(slug, token.EnClaro);
        var mensaje = plantillas.Restablecimiento(
            usuario.Correo, contextoTenant.Actual.RazonSocial, liga);

        // El tope vive aqui y no en el cliente HTTP porque es una decision de ESTE flujo:
        // el alta de una empresa si puede esperar diez segundos por Resend, esta
        // respuesta no, porque su duracion es informacion.
        using var acotado = CancellationTokenSource.CreateLinkedTokenSource(ct);
        acotado.CancelAfter(TopeEnvio);

        var envio = await correo.EnviarAsync(mensaje, acotado.Token);

        if (!envio.Enviado)
        {
            log.LogError(
                "Token de restablecimiento emitido en {Slug} pero el correo NO salio: "
                + "{Motivo}. La liga queda vigente {Vigencia}.",
                slug, envio.Detalle, PoliticaRestablecimiento.VigenciaTexto);

            return;
        }

        log.LogInformation("Liga de restablecimiento enviada en {Slug}.", slug);
    }

    /// <summary>
    /// Rellena hasta el piso. Con CancellationToken.None a proposito: si la espera se
    /// pudiera cancelar, abortar la peticion desde el cliente seria una forma de medir
    /// el tiempo real del trabajo, que es justo lo que el piso oculta.
    /// </summary>
    private static async Task EsperarAlPisoAsync(long inicio)
    {
        var restante = PisoDeRespuesta - Stopwatch.GetElapsedTime(inicio);

        if (restante > TimeSpan.Zero)
        {
            await Task.Delay(restante, CancellationToken.None);
        }
    }
}
