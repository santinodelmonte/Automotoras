using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Core.Tenants;

namespace AutomotoraSaaS.Tests.MultiTenancy;

public sealed class NormalizacionTests
{
    [Theory]
    [InlineData("automotoranorte.uy", "automotoranorte.uy")]
    [InlineData("AutomotoraNorte.UY", "automotoranorte.uy")]
    [InlineData("www.automotoranorte.uy", "automotoranorte.uy")]
    [InlineData("  automotoranorte.uy  ", "automotoranorte.uy")]
    [InlineData("automotoranorte.uy.", "automotoranorte.uy")]
    // El Host de un request trae el puerto cuando no es el 80 ni el 443. Sin sacarlo, un
    // dominio propio nunca resolvería en desarrollo.
    [InlineData("automotoranorte.uy:5173", "automotoranorte.uy")]
    public void El_dominio_se_normaliza_antes_de_buscarlo(string host, string esperado)
    {
        Assert.Equal(esperado, NombresDeDominio.Normalizar(host));
    }

    [Theory]
    [InlineData("automotoranorte.uy", true)]
    [InlineData("sub.automotoranorte.uy", true)]
    [InlineData("sinpunto", false)]
    [InlineData("-arranca-con-guion.uy", false)]
    [InlineData("https://automotoranorte.uy", false)]
    [InlineData("automotoranorte.uy/autos", false)]
    public void Un_dominio_mal_escrito_no_se_da_de_alta(string dominio, bool valido)
    {
        Assert.Equal(valido, NombresDeDominio.EsValido(NombresDeDominio.Normalizar(dominio)));
    }

    /// <summary>
    /// La misma normalización en el alta y en el login. Si no coincidieran, un usuario
    /// creado como <c>Juan@Norte.uy</c> quedaría sin poder entrar nunca.
    /// </summary>
    [Theory]
    [InlineData("Owner@Norte.uy", "owner@norte.uy")]
    [InlineData("  owner@norte.uy ", "owner@norte.uy")]
    public void El_email_se_normaliza_igual_en_el_alta_y_en_el_login(string escrito, string esperado)
    {
        Assert.Equal(esperado, Emails.Normalizar(escrito));
    }
}
