using AutomotoraSaaS.Core.Auth;
using AutomotoraSaaS.Infrastructure.MultiTenancy;

namespace AutomotoraSaaS.Tests.MultiTenancy;

public sealed class NormalizacionTests
{
    [Theory]
    [InlineData("automotoranorte.uy", "automotoranorte.uy")]
    [InlineData("AutomotoraNorte.UY", "automotoranorte.uy")]
    [InlineData("www.automotoranorte.uy", "automotoranorte.uy")]
    [InlineData("  automotoranorte.uy  ", "automotoranorte.uy")]
    public void El_dominio_se_normaliza_antes_de_buscarlo(string host, string esperado)
    {
        Assert.Equal(esperado, ResolvedorDeTenantPublico.NormalizarDominio(host));
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
