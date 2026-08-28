using Maquinaria.Infraestructura.Seguridad;

namespace Maquinaria.Api.Tests;

/// <summary>
/// El hashing es la pieza que no se puede probar en produccion, asi que se prueba
/// aqui. Son lentas a proposito: 600 mil iteraciones por llamada es el punto.
/// </summary>
public class HashContrasenasPruebas
{
    private readonly HashContrasenasPbkdf2 _hash = new();

    [Fact]
    public void La_contrasena_correcta_se_verifica()
    {
        var hash = _hash.Hash("Contrasena-Larga-Y-Buena-2026");

        var resultado = _hash.Verificar(hash, "Contrasena-Larga-Y-Buena-2026");

        Assert.True(resultado.EsValida);
        Assert.False(resultado.NecesitaRehash);
    }

    [Fact]
    public void La_contrasena_incorrecta_se_rechaza()
    {
        var hash = _hash.Hash("Contrasena-Larga-Y-Buena-2026");

        Assert.False(_hash.Verificar(hash, "Contrasena-Larga-Y-Buena-2027").EsValida);
    }

    [Fact]
    public void Dos_hashes_de_la_misma_contrasena_son_distintos()
    {
        // Si coincidieran, la sal no se estaria aplicando y dos usuarios con la misma
        // contrasena serian identificables entre si leyendo la base.
        Assert.NotEqual(_hash.Hash("misma"), _hash.Hash("misma"));
    }

    [Fact]
    public void El_formato_es_autodescriptivo()
    {
        // De esto depende que subir el costo no invalide los hashes existentes.
        var partes = _hash.Hash("x").Split('$');

        Assert.Equal(4, partes.Length);
        Assert.Equal("pbkdf2-sha256", partes[0]);
        Assert.True(int.Parse(partes[1]) >= 600_000);
    }

    [Theory]
    [InlineData("")]
    [InlineData("basura")]
    [InlineData("pbkdf2-sha256$no-es-numero$c2Fs$Y2xhdmU=")]
    [InlineData("otro-algoritmo$600000$c2Fs$Y2xhdmU=")]
    [InlineData("pbkdf2-sha256$600000$no-es-base64!$Y2xhdmU=")]
    public void Un_hash_corrupto_se_rechaza_sin_reventar(string almacenado)
    {
        // Un dato corrupto en la base no debe tumbar el login ni distinguirse de una
        // contrasena equivocada.
        var resultado = _hash.Verificar(almacenado, "cualquiera");

        Assert.False(resultado.EsValida);
        Assert.False(resultado.NecesitaRehash);
    }

    [Fact]
    public void Un_hash_con_costo_viejo_es_valido_pero_pide_rehash()
    {
        // Se simula un hash generado cuando las iteraciones eran menos. Debe seguir
        // verificando —si no, subir el costo dejaria a todos fuera— y debe pedir que
        // se regenere.
        var conCostoBajo = HashConIteraciones("Contrasena-Vieja", 100_000);

        var resultado = _hash.Verificar(conCostoBajo, "Contrasena-Vieja");

        Assert.True(resultado.EsValida);
        Assert.True(resultado.NecesitaRehash);
    }

    private static string HashConIteraciones(string contrasena, int iteraciones)
    {
        var sal = new byte[16];
        Random.Shared.NextBytes(sal);

        var clave = Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2(
            contrasena,
            sal,
            Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivationPrf.HMACSHA256,
            iteraciones,
            32);

        return string.Join('$', "pbkdf2-sha256", iteraciones, Convert.ToBase64String(sal),
            Convert.ToBase64String(clave));
    }
}
