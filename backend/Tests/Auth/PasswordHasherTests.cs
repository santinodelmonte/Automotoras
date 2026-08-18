using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Infrastructure.Auth;

namespace AutomotoraSaaS.Tests.Auth;

public sealed class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new PasswordHasherPbkdf2();

    [Fact]
    public void Una_contrasena_se_verifica_contra_su_propio_hash()
    {
        var hash = _hasher.Hash("Clave-de-prueba-1");

        Assert.True(_hasher.Verificar("Clave-de-prueba-1", hash));
    }

    [Fact]
    public void Una_contrasena_distinta_no_verifica()
    {
        var hash = _hasher.Hash("Clave-de-prueba-1");

        Assert.False(_hasher.Verificar("Clave-de-prueba-2", hash));
    }

    /// <summary>
    /// La sal es por contraseña, no global: dos usuarios con la misma clave tienen que
    /// tener hashes distintos, o una sola tabla precalculada los abre a los dos.
    /// </summary>
    [Fact]
    public void Dos_hashes_de_la_misma_contrasena_son_distintos()
    {
        var primero = _hasher.Hash("Clave-de-prueba-1");
        var segundo = _hasher.Hash("Clave-de-prueba-1");

        Assert.NotEqual(primero, segundo);
        Assert.True(_hasher.Verificar("Clave-de-prueba-1", primero));
        Assert.True(_hasher.Verificar("Clave-de-prueba-1", segundo));
    }

    /// <summary>
    /// El hash lleva adentro el algoritmo, las iteraciones y la sal. Es lo que permite
    /// subir el costo más adelante sin invalidar las contraseñas ya guardadas.
    /// </summary>
    [Fact]
    public void El_hash_declara_el_algoritmo_y_el_costo()
    {
        var partes = _hasher.Hash("Clave-de-prueba-1").Split('$');

        Assert.Equal(4, partes.Length);
        Assert.Equal("pbkdf2-sha256", partes[0]);
        Assert.True(int.Parse(partes[1], System.Globalization.CultureInfo.InvariantCulture) >= 100_000);
    }

    [Theory]
    [InlineData("")]
    [InlineData("cualquier-cosa")]
    [InlineData("pbkdf2-sha256$no-es-un-numero$c2Fs$aGFzaA==")]
    [InlineData("pbkdf2-sha256$1000$no-es-base64!$aGFzaA==")]
    [InlineData("otro-algoritmo$1000$c2Fs$aGFzaA==")]
    public void Un_hash_con_formato_invalido_no_verifica_y_no_revienta(string almacenado)
    {
        Assert.False(_hasher.Verificar("Clave-de-prueba-1", almacenado));
    }

    [Fact]
    public void La_verificacion_senuelo_no_lanza()
    {
        _hasher.VerificarSenuelo("Clave-de-prueba-1");
    }
}
