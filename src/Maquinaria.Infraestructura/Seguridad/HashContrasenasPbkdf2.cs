using System.Security.Cryptography;
using Maquinaria.Aplicacion.Plataforma;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Maquinaria.Infraestructura.Seguridad;

/// <summary>
/// PBKDF2-HMAC-SHA256, del paquete de Microsoft.
///
/// Se eligio sobre Argon2id por una razon de dependencias, no de criptografia:
/// Argon2 es hoy la primera recomendacion de OWASP, pero en .NET solo esta en
/// paquetes de terceros, y una dependencia de criptografia de terceros es la
/// categoria que mas cuidado exige auditar. PBKDF2 con 600 mil iteraciones sigue
/// siendo aceptable para OWASP y viene de Microsoft.
///
/// EL FORMATO ES AUTODESCRIPTIVO —lleva el algoritmo y las iteraciones dentro— para
/// que subir el costo en el futuro no invalide ni un hash existente: los viejos se
/// verifican con sus propios parametros y se rehashean en el siguiente login.
/// </summary>
public sealed class HashContrasenasPbkdf2 : IHashContrasenas
{
    private const string Etiqueta = "pbkdf2-sha256";

    /// <summary>
    /// 600 mil es la recomendacion de OWASP para PBKDF2-HMAC-SHA256. Subirla es una
    /// linea, y los hashes viejos siguen funcionando por el formato autodescriptivo.
    /// </summary>
    private const int IteracionesActuales = 600_000;

    private const int BytesSal = 16;
    private const int BytesClave = 32;

    /// <summary>
    /// Hash senuelo, calculado una sola vez al cargar la clase. Sirve para gastar el
    /// mismo tiempo cuando la cuenta no existe.
    /// </summary>
    private static readonly string HashSenuelo = new HashContrasenasPbkdf2()
        .Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public string Hash(string contrasena)
    {
        var sal = RandomNumberGenerator.GetBytes(BytesSal);

        var clave = KeyDerivation.Pbkdf2(
            password: contrasena,
            salt: sal,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: IteracionesActuales,
            numBytesRequested: BytesClave);

        return string.Join('$',
            Etiqueta,
            IteracionesActuales.ToString(),
            Convert.ToBase64String(sal),
            Convert.ToBase64String(clave));
    }

    public ResultadoVerificacion Verificar(string hashAlmacenado, string contrasena)
    {
        var partes = hashAlmacenado.Split('$');

        // Un hash con formato invalido se trata como no coincidente, nunca como
        // excepcion: un dato corrupto en la base no debe poder tumbar el login ni
        // distinguirse de una contrasena equivocada.
        if (partes.Length != 4 || partes[0] != Etiqueta
            || !int.TryParse(partes[1], out var iteraciones) || iteraciones <= 0)
        {
            return new ResultadoVerificacion(false, false);
        }

        byte[] sal, esperado;

        try
        {
            sal = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return new ResultadoVerificacion(false, false);
        }

        var calculado = KeyDerivation.Pbkdf2(
            password: contrasena,
            salt: sal,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: iteraciones,
            numBytesRequested: esperado.Length);

        // FixedTimeEquals y no SequenceEqual: comparar byte a byte con salida
        // temprana filtra, por tiempo, cuantos bytes iniciales acerto quien prueba.
        var coincide = CryptographicOperations.FixedTimeEquals(calculado, esperado);

        return new ResultadoVerificacion(coincide, coincide && iteraciones < IteracionesActuales);
    }

    public void VerificarSenuelo(string contrasena)
    {
        // El resultado se descarta a proposito: lo unico que interesa es haber gastado
        // el mismo tiempo que una verificacion real.
        _ = Verificar(HashSenuelo, contrasena);
    }
}
